using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media.Effects;

namespace NeuralCars
{
    class NeuralNet
    {

        public NeuralNet(int inputs, int outputs, int hidden)
        {
            Inputs = new List<Neuron>();
            for (int i = 0; i < inputs; i++) Inputs.Add(new Neuron());
            Hidden = new List<List<Neuron>>();
            for (int i = 0; i < hidden; i++)
            {
                var prevlayer = i == 0 ? Inputs : Hidden[i - 1];
                Hidden.Add(new List<Neuron>());
                for (int j = 0; j < prevlayer.Count; j++) Hidden[i].Add(new Neuron(prevlayer));
            }
            Outputs = new List<Neuron>();
            for (int i = 0; i < outputs; i++) Outputs.Add(new Neuron(Hidden[hidden - 1]) { Bias = 0.0});
        }

        public NeuralNet(NeuralNet progenitor)
        {
            Inputs = new List<Neuron>();
            Hidden = new List<List<Neuron>>();
            Outputs = new List<Neuron>();
            for (int i = 0; i < progenitor.Inputs.Count; i++) Inputs.Add(new Neuron());
            for (int i = 0; i < progenitor.Hidden.Count; i++)
            {
                var prevlayer = i == 0 ? Inputs : Hidden[i - 1];
                Hidden.Add(new List<Neuron>());
                for (int j = 0; j < prevlayer.Count; j++) Hidden[i].Add(progenitor.Hidden[i][j].Mutate(prevlayer));
            }
            for (int i = 0; i < progenitor.Outputs.Count; i++) Outputs.Add(progenitor.Outputs[i].Mutate(Hidden[Hidden.Count - 1]));
        }

        public NeuralNet(List<NeuralNet> lists)
        {
            Inputs = new List<Neuron>();
            Hidden = new List<List<Neuron>>();
            Outputs = new List<Neuron>();
            for (int i = 0; i < lists[0].Inputs.Count; i++) Inputs.Add(new Neuron());
            for (int i = 0; i < lists[0].Hidden.Count; i++)
            {
                var prevlayer = i == 0 ? Inputs : Hidden[i - 1];
                Hidden.Add(new List<Neuron>());
                for (int j = 0; j < prevlayer.Count; j++) {
                    var avg = new List<Neuron>();
                    for (int n = 0; n < lists.Count; n++) avg.Add(lists[n].Hidden[i][j]);
                    Hidden[i].Add(Neuron.Average(avg, prevlayer));
                }
            }
            for (int i = 0; i < lists[0].Outputs.Count; i++)
            {
                var avg = new List<Neuron>();
                for (int n = 0; n < lists.Count; n++) avg.Add(lists[n].Outputs[i]);
                Outputs.Add(Neuron.Average(avg, Hidden[Hidden.Count - 1]));
            }
        }

        public List<double> Update(List<double> inputs)
        {
            for (int i = 0; i < Inputs.Count; i++) Inputs[i].Activate(inputs[i]);
            for (int i = 0; i < Hidden.Count; i++) for (int j = 0; j < Hidden[i].Count; j++)
                {
                    Hidden[i][j].Calculate();
                }

            var res = new List<double>();
            for (int i = 0; i < Outputs.Count; i++) res.Add(Outputs[i].Calculate());
            return res;
        }
        public List<Neuron> Inputs { get; private set; }
        public List<List<Neuron>> Hidden { get; private set; }
        public List<Neuron> Outputs { get; private set; }
        internal NeuralNet Mutate()
        {
            return new NeuralNet(this);
        }

        internal static NeuralNet Average(List<NeuralNet> lists)
        {
            return new NeuralNet(lists);
        }
        public List<Neuron> GetLayer(int index)
        {
            if (index == 0) return Inputs;
            if (index == Hidden.Count + 1) return Outputs;
            return Hidden[index-1];
        }
        public int LayerCount => Hidden.Count + 2;
    }

    internal class Neuron
    {
        public enum Activation
        {
            Sigmoid,
            Tanh,
            Identity
        }
        private List<Neuron> prevlayer;

        public List<double> Weights { get; private set; }
        public Activation ActivationType { get; set; }
        public double Bias { get; set; }
        public double Value { get; set; }
        public double RangeMin
        {
            get
            {
                switch (ActivationType)
                {
                    case Activation.Sigmoid:
                        return 0.0;
                    case Activation.Tanh:
                        return -1.0;
                    default:
                        return 0.0;
                }
            }
        }
        public double RangeMax
        {
            get
            {
                switch (ActivationType)
                {
                    case Activation.Sigmoid:
                        return 1.0;
                    case Activation.Tanh:
                        return 1.0;
                    default:
                        return 1.0;
                }
            }
        }
        public double RangeCorrection => (RangeMax + RangeMin) / 2.0;

        public Neuron()
        {
        }

        public Neuron(List<Neuron> prevlayer)
        {
            this.prevlayer = prevlayer;
            Weights = new List<double>();
            var rnd = new Random();
            ActivationType = Activation.Tanh;
            for (int i = 0; i < prevlayer.Count; i++) Weights.Add(rnd.NextDouble() - 0.5);
            Bias = (rnd.NextDouble() - 0.5) * 0.1;
        }

        public Neuron(Neuron progenitor, List<Neuron> prevlayer)
        {
            var mutaterate = 20;
            var biasmutaterate = 300;
            var prob = 0.3;
            this.prevlayer = prevlayer;
            Weights = new List<double>();
            var rnd = new Random();
            for (int i = 0; i < prevlayer.Count; i++) Weights.Add(progenitor.Weights[i] + Mutate(rnd, mutaterate, prob));
            Bias = progenitor.Bias + Mutate(rnd, biasmutaterate, prob);
            ActivationType = progenitor.ActivationType;
        }

        private double Mutate(Random rnd, int mutaterate, double prob)
        {
            if (rnd.NextDouble() < prob) return (rnd.NextDouble() - 0.5) / mutaterate;
            return 0.0;
        }

        public Neuron(List<Neuron> avg, List<Neuron> prevlayer)
        {
            this.prevlayer = prevlayer;
            Weights = new List<double>();
            var rnd = new Random();
            for (int i = 0; i < prevlayer.Count; i++) {
                var w = 0.0;
                foreach (var v in avg) w += v.Weights[i];
                w /= avg.Count;
                Weights.Add(w);
            }
            Bias = 0;
            foreach (var v in avg) Bias += v.Bias;
            Bias /= avg.Count;
            ActivationType = avg[0].ActivationType;
        }

        public double Calculate()
        {
            var sum = 0.0;
            for (int i = 0; i < prevlayer.Count; i++) sum += Weights[i] * prevlayer[i].Value;
            sum += Bias;
            Activate(sum);
            return Value;
        }

        public void Activate(double sum)
        {
            switch (ActivationType)
            {
                case Activation.Sigmoid:
                    Value = Sigmoid(sum);
                    break;
                case Activation.Tanh:
                    Value = Math.Tanh(sum);
                    break;
                default:
                    Value = sum;
                    break;
            }
        }

        public static double Sigmoid(double value)
        {
            return (double)(1.0 / (1.0 + Math.Pow(Math.E, -value)));
        }

        internal Neuron Mutate(List<Neuron> prevlayer)
        {
            return new Neuron(this, prevlayer);
        }

        internal static Neuron Average(List<Neuron> avg, List<Neuron> prevlayer)
        {
            return new Neuron(avg, prevlayer);
        }
    }
}
