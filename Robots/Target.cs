﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhino.Geometry;
using System.Collections;
using static Robots.Util;

namespace Robots
{
    public class Target
    {
        public enum Motions { JointRotations, JointCartesian, Linear, Circular, Spline }
       [Flags] public enum RobotConfigurations { None = 0, Shoulder = 1, Elbow = 2, Wrist = 4}

        Plane plane;
        double[] jointRotations;

        public Plane Plane { get { return plane; } set {plane = value; jointRotations = null; } }
        public double[] JointRotations { get { return jointRotations; } set {jointRotations = value; plane = Plane.Unset; } }
        public Tool Tool { get; set; }
        public Motions Motion { get; set; }
        public Speed Speed { get; set; }
        public Zone Zone { get; set; }
        public Commands.Group Commands { get; set; } = new Commands.Group();
        public RobotConfigurations Configuration { get; set; }
        public bool IsCartesian => (JointRotations == null);

        public Target(Plane plane, Tool tool = null, Motions motion = Motions.JointCartesian, Speed speed = null, Zone zone = null, IEnumerable<Commands.ICommand> commands = null, RobotConfigurations configuration = 0)
        {
            this.Plane = plane;
            this.Tool = tool;
            this.Motion = motion;
            this.Speed = speed;
            this.Zone = zone;
            if (commands != null) Commands.AddRange(commands);
            this.Configuration = configuration;
        }

        public Target(double[] jointRotations, Tool tool = null, Speed speed = null, Zone zone = null, IEnumerable<Commands.ICommand> commands = null)
        {
            this.Plane = Plane.Unset;
            this.JointRotations = jointRotations;
            this.Motion = Motions.JointRotations;
            this.Tool = tool;
            this.Speed = speed;
            this.Zone = zone;
            if (commands != null) Commands.AddRange(commands);
        }

        public Target Duplicate()
        {
            if (IsCartesian)
                return new Target(Plane, Tool, Motion, Speed, Zone, Commands.ToList(), Configuration);
            else
                return new Target(JointRotations, Tool, Speed, Zone, Commands.ToList());
        }

        public override string ToString() => IsCartesian ? $"Target: Cartesian ({Plane.OriginX:0.00}, {Plane.OriginY:0.00}, {Plane.OriginZ:0.00})" : $"Target: Joint ({string.Join(",", JointRotations.Select(x=>$"{x:0.00}"))})";
    }

    public class Tool
    {
        public string Name { get; }
        public Plane Tcp { get; }
        public double Weight { get; }
        public Mesh Mesh { get; }

        public Tool(string name = "DefaultTool", Plane tcp = new Plane(), double weight = 0.01, Mesh mesh = null)
        {
            this.Name = name;
            this.Tcp = tcp;
            this.Weight = weight;
            this.Mesh = mesh;
        }
        public override string ToString() => $"Tool: {Name}";
    }

    public class Speed
    {
        public string Name { get; set; }
        public double TranslationSpeed { get; set; }
        public double RotationSpeed { get; set; }

        public Speed(double translation = 100, double rotation = 90)
        {
            this.TranslationSpeed = translation;
            this.RotationSpeed = rotation;
        }
        public Speed(string name, double translation = 100, double rotation = 90)
        {
            this.Name = name;
            this.TranslationSpeed = translation;
            this.RotationSpeed = rotation;
        }

        public override string ToString() => (Name != null)? $"Speed: {Name}" : $"Speed: {TranslationSpeed:0.00} mm/s";
    }

    public class Zone
    {
        public string Name { get; set; }
        public double Distance { get; set; }
        public double Rotation { get; set; }
        public bool IsFlyBy => Distance > Tol;

        public Zone(string name, double distance, double angle)
        {
            this.Name = name;
            this.Distance = distance;
            this.Rotation = angle;
        }

        public Zone(double distance = 0.3) : this(null, distance, distance) { }

        public override string ToString() => (Name != null) ? $"Zone: {Name}" : IsFlyBy ? $"Zone: {Distance:0.00} mm" : $"Zone: Stop point";
    }
}