﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kanvas.Quantization.Helper;
using Kanvas.Quantization.Interfaces;
using Kanvas.Quantization.Models;

namespace Kanvas.Quantization.Ditherers
{
    public abstract class BaseOrderDitherer : IColorDitherer
    {
        private IColorQuantizer _quantizer;
        private int _width;

        protected abstract byte[,] Matrix { get; }

        public void Prepare(IColorQuantizer quantizer, int height, int width)
        {
            _quantizer = quantizer;
            _width = width;
        }

        public IEnumerable<int> Process(IEnumerable<Color> colors)
        {
            var colorList = colors.ToArray();
            _quantizer.CreatePalette(colorList);

            void ProcessingAction(TaskModel<Color[], int[]> taskModel)
            {
                for (int i = taskModel.Start; i < taskModel.Start + taskModel.Length; i++)
                {
                    int x = i % _width % 4;
                    int y = i / _width % 4;

                    int threshold = Convert.ToInt32(Matrix[x, y]);
                    if (threshold > 0)
                    {
                        var color = taskModel.Input[i];

                        int red = GetClampedValue(color.R + threshold, 0, 255);
                        int green = GetClampedValue(color.G + threshold, 0, 255);
                        int blue = GetClampedValue(color.B + threshold, 0, 255);

                        taskModel.Output[i] = _quantizer.GetPaletteIndex(Color.FromArgb(color.A, red, green, blue));
                    }
                }
            }

            var indices = new int[colorList.Length];
            ParallelProcessing.ProcessList(colorList, indices, ProcessingAction, _quantizer.TaskCount);

            return indices;
        }

        private int GetClampedValue(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }
    }
}
