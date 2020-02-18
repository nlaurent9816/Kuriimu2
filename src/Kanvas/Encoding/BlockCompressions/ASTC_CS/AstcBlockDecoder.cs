﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Kanvas.Encoding.BlockCompressions.ASTC_CS.Colors;
using Kanvas.Encoding.BlockCompressions.ASTC_CS.Models;
using Kanvas.Support;
using Komponent.IO;
using Komponent.IO.Streams;
using Kontract.Models.IO;

namespace Kanvas.Encoding.BlockCompressions.ASTC_CS
{
    class AstcBlockDecoder
    {
        private IList<Color> ErrorColors =>
            Enumerable.Repeat(Constants.ErrorValue, _x * _y * _z).ToArray();

        private readonly int _x;
        private readonly int _y;
        private readonly int _z;

        public AstcBlockDecoder(int x, int y, int z)
        {
            _x = x;
            _y = y;
            _z = z;
        }

        public IEnumerable<Color> DecodeBlocks(byte[] block)
        {
            // Find block containing texel
            var br = CreateReader(block);

            // Read block mode
            var blockMode = BlockMode.Create(br);

            // If void-extent, return single color (optimization)
            if (blockMode.IsVoidExtent)
            {
                var voidExtentColor = CreateVoidExtentColor(br, blockMode.IsHdr);
                return Enumerable.Repeat(voidExtentColor, _x * _y * _z);
            }

            // If a reserved block is used, return error color
            if (blockMode.UsesReserved)
                return ErrorColors;

            // If invalid weight ranges
            if (blockMode.WeightCount > Constants.MaxWeightsPerBlock ||
                blockMode.WeightBitCount < Constants.MinWeightBitsPerBlock ||
                blockMode.WeightBitCount > Constants.MaxWeightBitsPerBlock)
                return ErrorColors;

            var partitions = br.ReadBits<int>(2) + 1;
            if (blockMode.IsDualPlane && partitions == 4)
                return ErrorColors;

            if (partitions == 1)
            {
                return DecodeSinglePartition(br, blockMode);
            }

            // TODO: Implement multi partition
            return Array.Empty<Color>();

            /* For each plane in image
                  If block mode requires infill
                    Find and decode stored weights adjacent to texel, unquantize and
                        interpolate
                  Else
                    Find and decode weight for texel, and unquantize

                Read number of partitions
                If number of partitions > 1
                  Read partition table pattern index
                  Look up partition number from pattern

                Read color endpoint mode and endpoint data for selected partition
                Unquantize color endpoints
                Interpolate color endpoints using weight (or weights in dual-plane mode)
                Return interpolated color
             */
        }

        private BitReader CreateReader(byte[] block)
        {
            var br = new BitReader(new ReverseStream(new MemoryStream(block),block.Length), BitOrder.LeastSignificantBitFirst, 1, ByteOrder.LittleEndian);
            return br;
        }

        private Color CreateVoidExtentColor(BitReader br, bool isHdr)
        {
            // Detect illegal void-extent
            br.Position--;
            if (br.ReadBits<byte>(2) != 3)
                return Constants.ErrorValue;

            // Detect illegal texture coordinates
            var minS = br.ReadBits<short>(13);
            var maxS = br.ReadBits<short>(13);
            var minT = br.ReadBits<short>(13);
            var maxT = br.ReadBits<short>(13);
            if (minS != 0x1FFF || maxS != 0x1FFF || minT != 0x1FFF || maxT != 0x1FFF)
            {
                if (minS >= maxS || minT >= maxT)
                {
                    return Constants.ErrorValue;
                }
            }

            // Read constant color block
            var r = br.ReadBits<int>(16);
            var g = br.ReadBits<int>(16);
            var b = br.ReadBits<int>(16);
            var a = br.ReadBits<int>(16);
            if (isHdr)
            {
                // In IsHdr mode, components are stored as FP16, or half-precision floating point
                return Color.FromArgb((int)(Half)a, (int)(Half)r, (int)(Half)g, (int)(Half)b);
            }

            // Otherwise they are stored as UNORM16, or short and therefore needs to be sampled down to a range of 0..255
            // UNORM16 is used due to the possibility of sRGB color space, which isn't supported currently
            return Color.FromArgb(
                Conversion.ChangeBitDepth(a, 16, 8),
                Conversion.ChangeBitDepth(r, 16, 8),
                Conversion.ChangeBitDepth(g, 16, 8),
                Conversion.ChangeBitDepth(b, 16, 8));
        }

        private IList<Color> DecodeSinglePartition(BitReader br, BlockMode blockMode)
        {
            var colorEndpointMode = ColorEndpointMode.Create(br);

            if (colorEndpointMode.EndpointValueCount > 18 || blockMode.IsHdr != colorEndpointMode.IsHdr)
                return ErrorColors;

            var colorBits = ColorHelper.CalculateColorBits(1, blockMode.WeightBitCount, blockMode.IsDualPlane, 0);
            var quantizationLevel = ColorHelper.QuantizationModeTable[colorEndpointMode.EndpointValueCount >> 1][colorBits];

            if (quantizationLevel < 4)
                return ErrorColors;

            var colorValues = IntegerSequenceEncoding.Decode(br, quantizationLevel, colorEndpointMode.EndpointValueCount);
            var colorEndpoints = ColorUnquantization.DecodeColorEndpoints(colorValues, colorEndpointMode.Format, quantizationLevel);

            // Weights decoding
            br.Position = 128 - blockMode.WeightBitCount;

            var result = new Color[_x * _y * _z];

            if (blockMode.IsDualPlane)
            {
                br.Position = 128 - blockMode.WeightBitCount - 2;
                var plane2ColorComponent = br.ReadBits<int>(2);

                var indices = IntegerSequenceEncoding.Decode(br, blockMode.QuantizationMode, blockMode.WeightCount);
                for (var i = 0; i < blockMode.WeightCount; i++)
                {
                    result[i] = ColorHelper.InterpolateColor(colorEndpoints, indices[i * 2], indices[i * 2 + 1], plane2ColorComponent);
                }
            }
            else
            {
                var indices = IntegerSequenceEncoding.Decode(br, blockMode.QuantizationMode, blockMode.WeightCount);
                for (var i = 0; i < blockMode.WeightCount; i++)
                {
                    result[i] = ColorHelper.InterpolateColor(colorEndpoints, indices[i], -1, -1);
                }
            }

            return result;
        }

        // TODO: Decode color endpoint values dependant on given partition count
        //private UInt4[][] DecodeColorEndpoints(BitReader br, BlockMode blockMode, int partition)
        //{
        //    var colorEndpointMode = ColorEndpointMode.Create(br);

        //    if (colorEndpointMode.EndpointValueCount > 18 || blockMode.IsHdr != colorEndpointMode.IsHdr)
        //        return ErrorColors;

        //    var colorBits = ColorHelper.CalculateColorBits(1, blockMode.WeightBitCount, blockMode.IsDualPlane, 0);
        //    var quantizationLevel = ColorHelper.QuantizationModeTable[colorEndpointMode.EndpointValueCount >> 1][colorBits];

        //    if (quantizationLevel < 4)
        //        return ErrorColors;

        //    var colorValues = IntegerSequenceEncoding.Decode(br, quantizationLevel, colorEndpointMode.EndpointValueCount);

        //    var colorEndpoints = ColorUnquantization.DecodeColorEndpoints(colorValues, colorEndpointMode.Format, quantizationLevel);
        //}
    }
}
