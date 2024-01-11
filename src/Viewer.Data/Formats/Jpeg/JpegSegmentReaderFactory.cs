﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viewer.Core;

namespace Viewer.Data.Formats.Jpeg
{
    public interface IJpegSegmentReaderFactory
    {
        /// <summary>
        /// Create JpegSegmentReader from given file path.
        /// </summary>
        /// <param name="filePath">Path to a file</param>
        /// <returns>JPEG segment reader</returns>
        IJpegSegmentReader CreateFromPath(string filePath);
    }

    [Export(typeof(IJpegSegmentReaderFactory))]
    public class JpegSegmentReaderFactory : IJpegSegmentReaderFactory
    {
        public IJpegSegmentReader CreateFromPath(string filePath)
        {
            return new JpegSegmentReader(
                new BinaryReader(
                    new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 0x1000, FileOptions.SequentialScan)));
        }
    }
}
