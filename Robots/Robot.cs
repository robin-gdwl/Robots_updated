﻿using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Linq;
using static Robots.Util;
using static System.Math;

namespace Robots
{
    [Serializable]
    public abstract partial class Robot
    {
        public enum Manufacturers { ABB, KUKA, UR };

        readonly string model;
        public Manufacturers Manufacturer { get; }
        public string Model { get { return $"{Manufacturer.ToString()}.{model}"; } }
        public string Extension { get; }
        public RobotIO IO { get; }

        protected Plane basePlane;
        readonly Mesh baseMesh;
        readonly Joint[] joints;

        public Mesh[] GetMeshes()
        {
            return joints.Select(x => x.Mesh).ToArray();
        }

        public Plane[] GetPlanes()
        {
            return joints.Select(x => x.Plane).ToArray();
        }


        internal Robot(string model, Manufacturers manufacturer, string extension, Plane basePlane, Mesh baseMesh, Joint[] joints, RobotIO io)
        {
            this.model = model;
            this.Manufacturer = manufacturer;
            this.Extension = extension;
            this.basePlane = basePlane;
            this.baseMesh = baseMesh;
            this.joints = joints;
            this.IO = io;

            KinematicSolution kinematics = null;
            if (manufacturer != Manufacturers.UR)
               kinematics = Kinematics(new Target(new double[] { 0, PI / 2, 0, 0, 0, -PI }), false);
            else
                kinematics = Kinematics(new Target(new double[] { -PI, -PI/2, 0, -PI / 2, 0, 0}), false);

            for (int i = 0; i < 6; i++)
            {
                joints[i].Plane = kinematics.Planes[i+1];
                joints[i].Range = new Interval(DegreeToRadian(joints[i].Range.T0, i), DegreeToRadian(joints[i].Range.T1, i));
            }
        }

        public static Robot Load(string model, Plane basePlane)
        {
            using (var stream = new MemoryStream(Properties.Resources.Robots))
            {
                var formatter = new BinaryFormatter();
                List<Robot> robots = formatter.Deserialize(stream) as List<Robot>;
                var robot = robots.First(x => x.Model == model);
                robot.basePlane = basePlane;
                return robot;
            }
        }

        public static List<string> List()
        {
            using (var stream = new MemoryStream(Properties.Resources.Robots))
            {
                var formatter = new BinaryFormatter();
                List<Robot> robots = formatter.Deserialize(stream) as List<Robot>;
                return robots.Select(x => x.Model).ToList();
            }
        }

        public static List<Robot> LoadFromFile()
        {
            var robots = new List<Robot>();

            XElement robotsData = XElement.Load($@"{ResourcesFolder}\robotsData.xml");
            Rhino.FileIO.File3dm robotsGeometry = Rhino.FileIO.File3dm.Read($@"{ResourcesFolder}\robotsGeometry.3dm");

            foreach (var robotData in robotsData.Elements())
            {
                var model = robotData.Attribute(XName.Get("model")).Value;
                var manufacturer = (Manufacturers)Enum.Parse(typeof(Manufacturers), robotData.Attribute(XName.Get("manufacturer")).Value);

                var robotLayer = robotsGeometry.Layers.First(x => x.Name == $"{manufacturer}.{model}");
                Mesh baseMesh = robotsGeometry.Objects.First(x => x.Attributes.LayerIndex == robotLayer.LayerIndex).Geometry as Mesh;

                var jointElements = robotData.Element(XName.Get("Joints")).Descendants().ToArray();
                Joint[] joints = new Joint[6];

                for (int i = 0; i < 6; i++)
                {
                    var jointElement = jointElements[i];
                    double a = Convert.ToDouble(jointElement.Attribute(XName.Get("a")).Value);
                    double d = Convert.ToDouble(jointElement.Attribute(XName.Get("d")).Value);
                    double minRange = Convert.ToDouble(jointElement.Attribute(XName.Get("minrange")).Value);
                    double maxRange = Convert.ToDouble(jointElement.Attribute(XName.Get("maxrange")).Value);
                    Interval range = new Interval(minRange, maxRange);
                    double maxSpeed = Convert.ToDouble(jointElement.Attribute(XName.Get("maxspeed")).Value);

                    var jointLayer = robotsGeometry.Layers.First(x => (robotLayer.Id == x.ParentLayerId) && (x.FullPath == $"{(i + 1):0}"));
                    Mesh mesh = robotsGeometry.Objects.First(x => x.Attributes.LayerIndex == jointLayer.LayerIndex).Geometry as Mesh;

                    joints[i] = new Joint() { A = a, D = d, Range = range, MaxSpeed = maxSpeed, Mesh = mesh };
                }

                var ioElement = robotData.Element(XName.Get("IO"));
                string[] doNames = ioElement.Element(XName.Get("DO")).Attribute(XName.Get("names")).Value.Split(',');
                string[] diNames = ioElement.Element(XName.Get("DI")).Attribute(XName.Get("names")).Value.Split(',');
                string[] aoNames = ioElement.Element(XName.Get("AO")).Attribute(XName.Get("names")).Value.Split(',');
                string[] aiNames = ioElement.Element(XName.Get("AI")).Attribute(XName.Get("names")).Value.Split(',');

                var io = new RobotIO() { DO = doNames, DI = diNames, AO = aoNames, AI = aiNames };

                Robot robot = null;

                switch (manufacturer)
                {
                    case (Manufacturers.ABB):
                        robot = new RobotABB(model, Plane.WorldXY, baseMesh, joints, io);
                        break;
                    case (Manufacturers.KUKA):
                        robot = new RobotKUKA(model, Plane.WorldXY, baseMesh, joints, io);
                        break;
                    case (Manufacturers.UR):
                        robot = new RobotUR(model, Plane.WorldXY, baseMesh, joints, io);
                        break;
                }
                robots.Add(robot);
            }

            return robots;
        }

        public static void Write()
        {
            var robots = LoadFromFile();
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, robots);
                File.WriteAllBytes($@"{ResourcesFolder}\Robots.rob", stream.ToArray());
            }
        }

        public virtual KinematicSolution Kinematics(Target target, bool calculateMeshes = true) => new SphericalWristKinematics(target, this, calculateMeshes);

        public override string ToString() => $"Robot: {Model}";

        abstract internal List<string> Code(Program program);
        public abstract double DegreeToRadian(double degree, int i);
        public abstract double RadianToDegree(double radian, int i);

        [Serializable]
        internal class Joint
        {
            public double A { get; set; }
            public double D { get; set; }
            public Interval Range { get; set; }
            public double MaxSpeed { get; set; }
            public Plane Plane { get; set; }
            public Mesh Mesh { get; set; }
        }

        [Serializable]
        public class RobotIO
        {
            public string[] DO { get; set; }
            public string[] DI { get; set; }
            public string[] AO { get; set; }
            public string[] AI { get; set; }
        }
    }
}