using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace NeuralCars
{
    class Racecar
    {
        public Vector position;
        private Vector direction;
        public double speed { get; private set; }
        private List<double> eyedirections;
        public List<Vector> seenpoints;

        public NeuralNet steeringnet { get; private set; }
        public bool IsStopped { get; private set; }
        public double DistanceTraveled { get; private set; }

        private RaceTrack raceTrack;
        public double directionchangerate { get; private set; }
        private int racestep;

        public Racecar(RaceTrack raceTrack) : this(raceTrack, new NeuralNet(5, 1, 2))
        {
        }

        public Racecar(RaceTrack raceTrack, NeuralNet steeringnet)
        {
            this.raceTrack = raceTrack;
            this.position = new Vector(raceTrack.data.start.x, raceTrack.data.start.y);
            direction = new Vector(0.0, 1.0);
            direction.Normalize();
            speed = 0.0;
            directionchangerate = 0.0;
            IsStopped = false;
            DistanceTraveled = 0.0;
            eyedirections = new List<double>()
            {
                45,
                20,
                0.0,
                -20,
                -45
            };
            seenpoints = new List<Vector>();
            this.steeringnet = steeringnet;
        }

        public Racecar Mutate()
        {
            return new Racecar(raceTrack, steeringnet.Mutate());
        }
        public void Race()
        {
            if (IsStopped) return;
            racestep++;
            bool outofstart = !IsPointInPolygon(raceTrack.startbox, position);
            if (racestep > 100 && !outofstart) IsStopped = true;
            Look();
            Steer();
            var movement = speed * direction;
            if(outofstart) DistanceTraveled += movement.Length;
            position += movement;
            if (IsPointInPolygon(raceTrack.stopbox, position)) IsStopped = true;
            foreach (var p in GetBox())
                if (!IsPointInPolygon(raceTrack.data.centerpath, p)) IsStopped = true;
        }

        private void Steer()
        {
            var inputs = new List<double>();
            for (int i = 0; i < seenpoints.Count; i++) inputs.Add(10.0 * position.Distance(seenpoints[i]));
            //inputs.Add(speed * 50.0);
            //inputs.Add(directionchangerate / 40.0 + 20.0);
            var res = steeringnet.Update(inputs);
            //var acceleration = res[0] - 0.5;
            var directionchange = res[0] - steeringnet.Outputs[0].RangeCorrection;
            speed = 0.002;
            /*
            speed += (acceleration / 10.0);
            speed = Math.Min(0.02, speed);
            speed = Math.Max(0.0, speed);
            */
            directionchangerate = directionchange * 30;
            if (directionchangerate > 20.0) directionchangerate = 20;
            if (directionchangerate < -20.0) directionchangerate = -20;
            direction = direction.RotateDegrees(directionchangerate);
        }

        private void Look()
        {
            seenpoints.Clear();
            foreach (var eye in eyedirections)
                seenpoints.Add(Look(eye));
        }

        private Vector Look(double eye)
        {
            var dir = direction.RotateDegrees(eye);
            var lookp = position + dir;
            var lookLine = LineEquation.GetLineEquation(position, lookp);
            var closest = new Vector(5.0, 5.0);
            foreach (var seg in raceTrack.BoundarySegments)
            {
                var res = seg.FindIntersect(lookLine);
                if (res.Intersects &&
                    res.Location.WithinBox(position, lookp) &&
                    position.DistanceSquare(res.Location) < position.DistanceSquare(closest)) closest = res.Location;
            }
            return closest;
        }

        internal Racecar Restart()
        {
            return new Racecar(raceTrack, steeringnet);
        }

        private bool IsPointInPolygon(List<(double x, double y)> startbox, Vector position)
        {
            var poly = new List<Vector>();
            foreach (var p in startbox) poly.Add(new Vector(p.x, p.y));
            return IsPointInPolygon(poly, position);
        }

        /// <summary>
        /// Ray cast method from https://stackoverflow.com/questions/4243042/c-sharp-point-in-polygon
        /// </summary>
        public static bool IsPointInPolygon(List<Vector> polygon, Vector testPoint)
        {
            bool result = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                if (polygon[i].Y < testPoint.Y && polygon[j].Y >= testPoint.Y || polygon[j].Y < testPoint.Y && polygon[i].Y >= testPoint.Y)
                {
                    if (polygon[i].X + (testPoint.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) * (polygon[j].X - polygon[i].X) < testPoint.X)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        internal List<Vector> GetBox()
        {
            var res = new List<Vector>();
            var forward = direction * 0.02;
            var side = direction.RotateDegrees(90) * 0.01;
            res.Add(position + forward + side);
            res.Add(position + forward - side);
            res.Add(position - forward - side);
            res.Add(position - forward + side);
            return res;
        }
    }
}
