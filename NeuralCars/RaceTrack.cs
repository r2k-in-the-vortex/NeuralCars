using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NeuralCars
{
    class RaceTrack
    {
        public struct saveddata
        {
            public List<(double x, double y)> centerpath;
            public (double x, double y) start;
            public (double x, double y) stop;
            public double boxSize;
        }
        internal saveddata data = new saveddata();
        internal List<(double x, double y)> startbox;
        internal List<(double x, double y)> stopbox;
        private string dataFilename;
        private List<Racecar> cars;
        public List<BoundarySegment> BoundarySegments;

        public int CarCount { get; private set; }
        public int Generation { get; private set; }
        public double MaxDistance { get; private set; }
        public int LastImproveGeneration { get; private set; }
        public Racecar AverageWinner { get; private set; }

        public RaceTrack(string dataFilename)
        {
            this.dataFilename = dataFilename;
            data = RestoreData(dataFilename);
            cars = new List<Racecar>();
            startbox = GetBox(data.start, data.boxSize);
            stopbox = GetBox(data.stop, data.boxSize);
            BoundarySegments = SetupBoundarySegments();
            CarCount = 50;
            for (int i = 0; i < CarCount; i++) cars.Add(new Racecar(this));
        }

        private List<BoundarySegment> SetupBoundarySegments()
        {
            var res = new List<BoundarySegment>();
            for (int i = 0; i < data.centerpath.Count; i++)
            {
                var line = BoundarySegment.GetBoundarySegment(data.centerpath[i], data.centerpath[(i + 1) % data.centerpath.Count]);
                res.Add(line);
            }
            return res;
        }

        private saveddata RestoreData(string dataFilename)
        {
            if (File.Exists(dataFilename))
            {
                return JsonConvert.DeserializeObject<saveddata>(File.ReadAllText(dataFilename));
            }
            return new saveddata() { boxSize = 0.1, start = (0.1, 0.1), stop = (0.9, 0.9), centerpath = new List<(double x, double y)>() };
        }
        public void DrawRacetrack(Canvas canvas)
        {
            bool updatevisual = true;// (Generation % 10) == 0;
            Parallel.ForEach(cars, e => e.Race());
            if (updatevisual) canvas.Children.Clear();
            DrawTextInfo(canvas, updatevisual);
            DrawBoundary(canvas,updatevisual);
            DrawBox(canvas, startbox,updatevisual);
            DrawBox(canvas, stopbox, updatevisual);
            if (cars.Count(e => !e.IsStopped) == 0) NextGeneration();
            foreach (var car in cars) DrawCar(canvas, car, updatevisual);
            DrawSeenpoints(canvas, cars[0], updatevisual);
            DrawLeadCarSteering(canvas, cars[0].steeringnet);
        }

        private void DrawLeadCarSteering(Canvas canvas, NeuralNet steeringnet)
        {
            (double x1, double x2, double y1, double y2) box = (0.3, 0.95, 0.05, 0.3);
            var layers = steeringnet.LayerCount;
            for(int l = 1; l < 4; l++)
            {
                var current = steeringnet.GetLayer(l);
                var prev = steeringnet.GetLayer(l - 1);
                var x1 = FromRangeToRange(l, 0, layers - 1, box.x1, box.x2);
                var x2 = FromRangeToRange(l - 1, 0, layers - 1, box.x1, box.x2);
                for (int i = 0; i< current.Count; i++)
                {
                    var y1 = FromRangeToRange(i, 0, current.Count - 1, box.y1, box.y2);
                    for (int j = 0; j < prev.Count; j++)
                    {
                        var value = (current[i].Weights[j] * prev[j].Value) * 50;
                        var color = value < 0 ? Brushes.Red : Brushes.Green;
                        value = Math.Abs(value);
                        value = Math.Min(10, value);
                        var y2 = FromRangeToRange(j, 0, prev.Count - 1, box.y1, box.y2);
                        var line = GetLinesegment(canvas.RenderSize, (x1, y1), (x2, y2));
                        line.Stroke = color;
                        line.StrokeThickness = value < 1 ? 1 : value;
                        canvas.Children.Add(line);
                    }
                }
            }
        }
        private static double FromRangeToRange(double value, double istart, double istop, double ostart, double ostop)
        {
            if (istart == istop) return (ostart + ostop) / 2;
            return ostart + ((ostop - ostart) / (istop - istart)) * (value - istart);
        }
        private void DrawTextInfo(Canvas sender, bool updatevisual)
        {
            if (!updatevisual) return;
            TextBlock textBlock = new TextBlock();
            textBlock.Text = $"Generation: {Generation}\r\nMax Distance Traveled: {MaxDistance}\r\nLead car speed: {cars[0].speed}\r\nLead car turn rate: {cars[0].directionchangerate}\r\nLast Improve Generation{LastImproveGeneration}";
            textBlock.Foreground = Brushes.Black;
            Canvas.SetLeft(textBlock, 0);
            Canvas.SetTop(textBlock, 0);
            sender.Children.Add(textBlock);
        }

        private void NextGeneration()
        {
            Generation++;
            var winners = cars.OrderByDescending(i => i.DistanceTraveled).Take(3).ToList();
            cars.Clear();
            cars.Add(winners[0].Restart());
            var prevDist = MaxDistance;
            MaxDistance = winners[0].DistanceTraveled;
            if (MaxDistance > prevDist) LastImproveGeneration = Generation;
            AverageWinner = new Racecar(this, NeuralNet.Average(new List<NeuralNet>() { winners[0].steeringnet, winners[1].steeringnet, winners[2].steeringnet }));
            cars.Add(AverageWinner.Restart());
            for (int i = 1; i < CarCount; i++) cars.Add(AverageWinner.Mutate());
        }

        private void DrawBox(Canvas sender, List<(double x, double y)> box, bool updatevisual)
        {
            if (!updatevisual) return;
            for (int i = 0; i < box.Count; i++)
            {
                var line = GetLinesegment(sender.RenderSize, box[i], box[(i + 1) % box.Count]);
                line.Stroke = Brushes.Red;
                line.StrokeThickness = 2;
                sender.Children.Add(line);
            }
        }
        private void DrawSeenpoints(Canvas sender, Racecar car, bool updatevisual)
        {
            if (!updatevisual) return;
            if (!car.IsStopped) foreach (var p in car.seenpoints)
                {
                    var line = GetLinesegment(sender.RenderSize, car.position, p);
                    line.Stroke = Brushes.Black;
                    line.StrokeThickness = 1;
                    sender.Children.Add(line);
                }
        }
        private void DrawCar(Canvas sender, Racecar car, bool updatevisual)
        {
            if (!updatevisual) return;
            var box = car.GetBox();
            var color = car.IsStopped ? Brushes.Red : Brushes.Green;
            for (int i = 0; i < box.Count; i++)
            {
                var line = GetLinesegment(sender.RenderSize, box[i], box[(i + 1) % box.Count]);
                line.Stroke = color;
                line.StrokeThickness = 2;
                sender.Children.Add(line);
            }
            
        }

        private List<(double x, double y)> GetBox((double x, double y) P, double boxSize)
        {
            List<(double x, double y)> box = new List<(double x, double y)>();
            var l = boxSize / 2.0;
            box.Add((P.x + l, P.y + l));
            box.Add((P.x + l, P.y - l));
            box.Add((P.x - l, P.y - l));
            box.Add((P.x - l, P.y + l));

            return box;
        }

        private void DrawBoundary(Canvas sender, bool updatevisual)
        {
            if (!updatevisual) return;
            if (data.centerpath.Count == 0) return;
            for (int i = 0; i < data.centerpath.Count; i++)
            {
                var line = GetLinesegment(sender.RenderSize, data.centerpath[i], data.centerpath[(i + 1) % data.centerpath.Count]);
                line.Stroke = Brushes.LightSteelBlue;
                line.StrokeThickness = 2;
                sender.Children.Add(line);
            }
        }
        private void SaveData()
        {
            System.IO.File.WriteAllText(dataFilename, JsonConvert.SerializeObject(data));
        }
        private static Line GetLinesegment(Size renderSize, (double x, double y) p1, (double x, double y) p2)
        {
            Point Start = GetRenderPoint(renderSize, p1);
            Point Stop = GetRenderPoint(renderSize, p2);
            var ret = new Line();

            ret.X1 = Start.X;
            ret.X2 = Stop.X;
            ret.Y1 = Start.Y;
            ret.Y2 = Stop.Y;

            return ret;
        }

        private static Line GetLinesegment(Size renderSize, Vector p1, Vector p2)
        {
            return GetLinesegment(renderSize, (p1.X, p1.Y), (p2.X, p2.Y));
        }

        internal void AddPathPoint(Canvas canvas, Point point)
        {
            data.centerpath.Add(GetNormalizedPoint(canvas.RenderSize, point));
            DrawRacetrack(canvas);
            SaveData();
        }
        internal void SetStart(Canvas canvas, Point point)
        {
            data.start = GetNormalizedPoint(canvas.RenderSize, point);
            startbox = GetBox(data.start, data.boxSize);
            DrawRacetrack(canvas);
            SaveData();
        }
        internal void SetStop(Canvas canvas, Point point)
        {
            data.stop = GetNormalizedPoint(canvas.RenderSize, point);
            stopbox = GetBox(data.stop, data.boxSize);
            DrawRacetrack(canvas);
            SaveData();
        }
        private static (double x, double y) GetNormalizedPoint(Size renderSize, Point point)
        {
            return (point.X / renderSize.Width, point.Y / renderSize.Height);
        }
        private static Point GetRenderPoint(Size renderSize, (double x, double y) p)
        {
            return new Point(renderSize.Width * p.x, renderSize.Height * p.y);
        }

    }
    public struct BoundarySegment
    {
        public Vector Min;
        public Vector Max;
        public LineEquation Line;
        public (bool Intersects, Vector Location) FindIntersect(LineEquation l)
        {
            var intersect = Line.Intersect(l);
            return (PointWithinBox(intersect), intersect);
        }
        public bool PointWithinBox(Vector p1)
        {
            return p1.WithinBox(Min, Max);
        }
        public static BoundarySegment GetBoundarySegment(Vector p1, Vector p2)
        {
            return new BoundarySegment() { Line = LineEquation.GetLineEquation(p1, p2), Min = new Vector(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y)), Max = new Vector(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y)) };
        }

        internal static BoundarySegment GetBoundarySegment((double x, double y) p1, (double x, double y) p2)
        {
            return GetBoundarySegment(new Vector(p1.x, p1.y), new Vector(p2.x, p2.y));
        }
    }
    public struct LineEquation
    {
        public double A;
        public double B;
        public double C;
        /// <summary>
        /// returns Ax + By = C
        /// </summary>
        public static LineEquation GetLineEquation(Vector p1, Vector p2)
        {
            //(y1- y2)x + (x2 - x1)y + (x1y2 - x2y1) = 0
            //(y1- y2)x + (x2 - x1)y = x2y1 -x1y2
            return new LineEquation() { A = p1.Y - p2.Y, B = p2.X - p1.X, C = p2.X * p1.Y - p1.X * p2.Y };
        }
        public Vector Intersect(LineEquation l2)
        {
            return Intersect(this, l2);
        }
        public static Vector Intersect(LineEquation l1, LineEquation l2)
        {
            double delta = l1.A * l2.B - l2.A * l1.B;

            if (delta == 0)
                return new Vector();

            double x = (l2.B * l1.C - l1.B * l2.C) / delta;
            double y = (l1.A * l2.C - l2.A * l1.C) / delta;
            return new Vector(x, y);
        }
    }
    public static class VectorExt
    {
        private const double DegToRad = Math.PI / 180;
        public static bool WithinBox(this Vector p, Vector p1, Vector p2)
        {
            var xmin = Math.Min(p1.X, p2.X);
            var xmax = Math.Max(p1.X, p2.X);
            if (xmin == xmax)
            {
                xmin -= 0.00001;
                xmax += 0.00001;
            }
            var ymin = Math.Min(p1.Y, p2.Y);
            var ymax = Math.Max(p1.Y, p2.Y);
            if (ymin == ymax)
            {
                ymin -= 0.00001;
                ymax += 0.00001;
            }
            return p.X >= xmin && p.X <= xmax && p.Y >= ymin && p.Y <= ymax;
        }
        public static Vector RotateDegrees(this Vector v, double degrees)
        {
            return v.Rotate(degrees * DegToRad);
        }
        public static double DistanceSquare(this Vector v, Vector v1)
        {
            var x = v.X - v1.X;
            var y = v.Y - v1.Y;
            return x * x + y * y;
        }
        public static double Distance(this Vector v, Vector v1)
        {
            return Math.Sqrt(v.DistanceSquare(v1));
        }
        public static double Angle(this Vector v)
        {
            return Math.Atan(v.Y / v.X);
        }
        public static Vector Rotate(this Vector v, double radians)
        {
            var ca = Math.Cos(radians);
            var sa = Math.Sin(radians);
            return new Vector(ca * v.X - sa * v.Y, sa * v.X + ca * v.Y);
        }
    }
}
