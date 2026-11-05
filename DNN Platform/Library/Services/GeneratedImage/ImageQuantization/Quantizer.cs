// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage.ImageQuantization
{
    using System;
    using System.Runtime.InteropServices;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;

    /// <summary>Abstract class for Quantizers.</summary>
    /// <remarks>
    /// <para>
    /// Ported from GDI+ to ImageSharp. The two-pass structure, the virtual/abstract members and the
    /// <see cref="Color32"/> helper struct are all preserved so that a derived quantizer keeps
    /// working. What changed:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Quantize"/> returns an ImageSharp <see cref="Image{Rgba32}"/> instead of a
    /// <c>System.Drawing.Bitmap</c>.
    /// </description></item>
    /// <item><description>
    /// <c>Bitmap.LockBits</c>/<c>BitmapData</c>/<c>Marshal</c> pointer walking is replaced by
    /// ImageSharp's pixel rows — there is no unmanaged memory to pin or unlock.
    /// </description></item>
    /// <item><description>
    /// <c>GetPalette(ColorPalette)</c> became <see cref="GetPalette(Color[])"/>: modern .NET has no
    /// <c>ColorPalette</c>, and ImageSharp images are not palette-indexed. The quantized result is
    /// therefore written as the palette color for each pixel; encoding it as a truly indexed image is
    /// the job of the (GIF/PNG) encoder's own quantizer.
    /// </description></item>
    /// </list>
    /// </remarks>
    public abstract class Quantizer
    {
        /// <summary>The number of palette entries the quantizers work with (an 8bpp palette).</summary>
        private const int PaletteSize = 256;

        /// <summary>Flag used to indicate whether a single pass or two passes are needed for quantization.</summary>
        private bool singlePass;

        /// <summary>
        /// Initializes a new instance of the <see cref="Quantizer"/> class.
        /// Construct the quantizer.
        /// </summary>
        /// <param name="singlePass">If true, the quantization only needs to loop through the source pixels once.</param>
        /// <remarks>
        /// If you construct this class with a true value for singlePass, then the code will, when quantizing your image,
        /// only call the 'QuantizeImage' function. If two passes are required, the code will call 'InitialQuantizeImage'
        /// and then 'QuantizeImage'.
        /// </remarks>
        public Quantizer(bool singlePass)
        {
            this.singlePass = singlePass;
        }

        /// <summary>Quantize an image and return the resulting output image.</summary>
        /// <param name="source">The image to quantize.</param>
        /// <returns>A quantized version of the image.</returns>
        public Image<Rgba32> Quantize(Image source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // First off take a 32bpp copy of the image, which is what Color32 expects to read.
            using var copy = source.CloneAs<Rgba32>();

            // Call the FirstPass function if not a single pass algorithm.
            // For something like an octree quantizer, this will run through
            // all image pixels, build a data structure, and create a palette.
            if (!this.singlePass)
            {
                this.FirstPass(copy, copy.Width, copy.Height);
            }

            // Then ask the derived quantizer for the palette. The incoming array stands in for the
            // "any old palette, this is overwritten" argument the GDI+ version was handed.
            var palette = this.GetPalette(new Color[PaletteSize]);

            // Then call the second pass which actually does the conversion.
            var output = new Image<Rgba32>(copy.Width, copy.Height);
            this.SecondPass(copy, output, copy.Width, copy.Height, palette);

            // Last but not least, return the output image
            return output;
        }

        /// <summary>Execute the first pass through the pixels in the image.</summary>
        /// <param name="sourceData">The source image.</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        protected virtual void FirstPass(Image<Rgba32> sourceData, int width, int height)
        {
            sourceData.ProcessPixelRows(accessor =>
            {
                // Loop through each row
                for (var row = 0; row < height; row++)
                {
                    var sourceRow = accessor.GetRowSpan(row);

                    // And loop through each column
                    for (var col = 0; col < width; col++)
                    {
                        // Now I have the pixel, call the FirstPassQuantize function...
                        this.InitialQuantizePixel(new Color32(sourceRow[col]));
                    }
                }
            });
        }

        /// <summary>Execute a second pass through the image.</summary>
        /// <param name="sourceData">The source image.</param>
        /// <param name="output">The output image.</param>
        /// <param name="width">The width in pixels of the image.</param>
        /// <param name="height">The height in pixels of the image.</param>
        /// <param name="palette">The palette produced by <see cref="GetPalette(Color[])"/>.</param>
        protected virtual void SecondPass(Image<Rgba32> sourceData, Image<Rgba32> output, int width, int height, Color[] palette)
        {
            var palettePixels = new Rgba32[palette.Length];
            for (var i = 0; i < palette.Length; i++)
            {
                palettePixels[i] = palette[i].ToPixel<Rgba32>();
            }

            sourceData.ProcessPixelRows(output, (sourceAccessor, outputAccessor) =>
            {
                var previousPixel = default(Color32);
                var havePrevious = false;
                byte pixelValue = 0;

                // Loop through each row
                for (var row = 0; row < height; row++)
                {
                    var sourceRow = sourceAccessor.GetRowSpan(row);
                    var destinationRow = outputAccessor.GetRowSpan(row);

                    // Loop through each pixel on this scan line
                    for (var col = 0; col < width; col++)
                    {
                        var pixel = new Color32(sourceRow[col]);

                        // Check if this is the same as the last pixel. If so use that value
                        // rather than calculating it again. This is an inexpensive optimisation.
                        if (!havePrevious || previousPixel.ARGB != pixel.ARGB)
                        {
                            // Quantize the pixel
                            pixelValue = this.QuantizePixel(pixel);

                            // And setup the previous pixel
                            previousPixel = pixel;
                            havePrevious = true;
                        }

                        // And set the pixel in the output
                        destinationRow[col] = palettePixels[pixelValue];
                    }
                }
            });
        }

        /// <summary>Override this to process the pixel in the first pass of the algorithm.</summary>
        /// <param name="pixel">The pixel to quantize.</param>
        /// <remarks>
        /// This function need only be overridden if your quantize algorithm needs two passes,
        /// such as an Octree quantizer.
        /// </remarks>
        protected virtual void InitialQuantizePixel(Color32 pixel)
        {
        }

        /// <summary>Override this to process the pixel in the second pass of the algorithm.</summary>
        /// <param name="pixel">The pixel to quantize.</param>
        /// <returns>The quantized value.</returns>
        protected abstract byte QuantizePixel(Color32 pixel);

        /// <summary>Retrieve the palette for the quantized image.</summary>
        /// <param name="original">Any old palette, this is overrwritten.</param>
        /// <returns>The new color palette.</returns>
        protected abstract Color[] GetPalette(Color[] original);

        /// <summary>Struct that defines a 32 bpp colour.</summary>
        /// <remarks>
        /// The explicit layout (and therefore the meaning of <see cref="ARGB"/>) is unchanged from
        /// the GDI+ version; it is now populated from an ImageSharp <see cref="Rgba32"/> pixel
        /// rather than from a pointer into locked bitmap bits.
        /// </remarks>
        [StructLayout(LayoutKind.Explicit)]
        public struct Color32
        {
            /// <summary>Holds the blue component of the colour.</summary>
            [FieldOffset(0)]
            public byte Blue;

            /// <summary>Holds the green component of the colour.</summary>
            [FieldOffset(1)]
            public byte Green;

            /// <summary>Holds the red component of the colour.</summary>
            [FieldOffset(2)]
            public byte Red;

            /// <summary>Holds the alpha component of the colour.</summary>
            [FieldOffset(3)]
            public byte Alpha;

            /// <summary>Permits the color32 to be treated as an int32.</summary>
            [FieldOffset(0)]
            public int ARGB;

            /// <summary>Initializes a new instance of the <see cref="Color32"/> struct.</summary>
            /// <param name="pixel">The source pixel.</param>
            public Color32(Rgba32 pixel)
            {
                // ARGB overlaps the four bytes (as it did in the GDI+ version), so assigning the
                // components is all that is needed.
                this.ARGB = 0;
                this.Blue = pixel.B;
                this.Green = pixel.G;
                this.Red = pixel.R;
                this.Alpha = pixel.A;
            }

            /// <summary>Gets return the color for this Color32 object.</summary>
            public Color Color
            {
                get { return Color.FromRgba(this.Red, this.Green, this.Blue, this.Alpha); }
            }
        }
    }
}
