using iTextSharp.text;
using iTextSharp.text.pdf;
using PDFiumSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;



namespace Pdf2Image
{

    public static class PdfSplitter
    {
        private static PdfReader Reader { get; set; } = null;
        public enum Scale
        {
            Low=1,
            High =2,
            VeryHigh=3            
        }

        public enum CompressionLevel : long
        {
            High = 25L,
            Medium = 50L,
            Low = 90L,
            None = 100L
        }

        #region Public Methods
        /// <summary>
        /// Return a System.Drawing.Image collection with all the pdf pages
        /// </summary>
        /// <param name="file">PDF file system source path</param>
        /// <param name="scale">Image resolution. Higher resolution generates bigger Image objects </param>
        /// <returns></returns>
        public static List<System.Drawing.Image> GetImages(string file, Scale scale, List<int> pagenumbers = null)
        {
            if (File.Exists(file) && Path.GetExtension(file).ToLower() == ".pdf")
            {
                pagenumbers = pagenumbers ?? new List<int>();
                if (!File.Exists(file)) return new List<System.Drawing.Image>();
                Reader = new PdfReader(file);
                return ProcessPdfToMemory(scale, pagenumbers);
            }
            return new List<System.Drawing.Image>();
        }

        /// <summary>
        /// Return a System.Drawing.Image collection with all the pdf pages
        /// </summary>
        /// <param name="file">contents of the file in a byte array</param>
        /// <param name="scale">Image resolution. Higher resolution generates bigger Image objects </param>
        /// <returns></returns>
        public static List<System.Drawing.Image> GetImages(byte[] file, Scale scale, List<int> pagenumbers = null)
        {
            pagenumbers = pagenumbers ?? new List<int>();
            if (file == null) return new List<System.Drawing.Image>();
            Reader = new PdfReader(file);
            return ProcessPdfToMemory(scale, pagenumbers);
        }

        /// <summary>
        /// Writes on outputFolder all the pdf pages as images with the resolution and format specified
        /// </summary>
        /// <param name="file">PDF file system source path</param>
        /// <param name="outputFolder">Folder where the images will be generated</param>
        /// <param name="scale">Image resolution. Higher resolution generates bigger Image files</param>
        /// <param name="format">Image format</param>
        public static void WriteImages(string file, string outputFolder, Scale scale, CompressionLevel compression, List<int> pagenumbers = null)
        {
            if (File.Exists(file) && Path.GetExtension(file).ToLower() == ".pdf")
            {
                if (!Directory.Exists(outputFolder) || !File.Exists(file)) return;
                var filename = Path.GetFileNameWithoutExtension(file);
                PdfReader.AllowOpenWithFullPermissions = true;
                Reader = new PdfReader(file);
                ProcessPDF2Filesystem(outputFolder, scale, compression, filename, pagenumbers);
            }
        }

        /// <summary>
        /// Writes on outputFolder all the pdf pages as images with the resolution and format specified. Because the filename if not provided, all the generated files in outputFolder will start with "pdfpic"
        /// </summary>
        /// <param name="file">contents of the file in a byte array</param>
        /// <param name="outputFolder">Folder where the images will be generated</param>
        /// <param name="scale">Image resolution. Higher resolution generates bigger Image files</param>
        /// <param name="format">Image format</param>
        public static void WriteImages(byte[] file, string outputFolder, Scale scale, CompressionLevel compression, string filename = "pdfpic",  List<int> pagenumbers = null)
        {            
            if (file == null) return;            
            PdfReader.AllowOpenWithFullPermissions = true;
            Reader = new PdfReader(file);
            ProcessPDF2Filesystem(outputFolder, scale, compression, filename, pagenumbers);
        }

        /// <summary>
        /// Return a System.Drawing.Image collection with all the images in the pdf
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static List<byte[]> ExtractJpeg(string file)
        {
            List<byte[]> images = new List<byte[]>();
            if (File.Exists(file) && Path.GetExtension(file).ToLower() == ".pdf")
            {
                var pdf = new PdfReader(file);
                int n = pdf.NumberOfPages;
                for (int i = 1; i <= n; i++)
                {
                    var pg = pdf.GetPageN(i);
                    var res = PdfReader.GetPdfObject(pg.Get(PdfName.Resources)) as PdfDictionary;
                    var xobj = PdfReader.GetPdfObject(res.Get(PdfName.Xobject)) as PdfDictionary;
                    if (xobj == null) continue;

                    var keys = xobj.Keys;

                    if (keys.Count == 0) continue;
                    var foo = keys.OfType<PdfName>().FirstOrDefault();
                    PdfObject obj = xobj.Get(foo);
                    if (!obj.IsIndirect()) continue;

                    var tg = PdfReader.GetPdfObject(obj) as PdfDictionary;
                    var type = PdfReader.GetPdfObject(tg.Get(PdfName.Subtype)) as PdfName;
                    if (!PdfName.Image.Equals(type)) continue;

                    int XrefIndex = (obj as PrIndirectReference).Number;
                    var pdfStream = pdf.GetPdfObject(XrefIndex) as PrStream;
                    var data = PdfReader.GetStreamBytesRaw(pdfStream);
                    images.Add(data);
                }
                return images;
            }
            return images;
        }

        /// <summary>
        /// Writes on outputFolder all the images in the pdf
        /// </summary>
        /// <param name="file"></param>
        /// <param name="outputfolder"></param>
        public static void ExtractJpeg(string file, string outputfolder)
        {
            if (File.Exists(file) && Path.GetExtension(file).ToLower() == ".pdf")
            {
                var fn = Path.GetFileNameWithoutExtension(file);
                List<byte[]> images = ExtractJpeg(file);

                for (int i = 0; i < images.Count; i++)
                {
                    var tmpfn = $"{fn}_{string.Format("{0:0000}.jpg", i)}";
                    var jpeg = Path.Combine(outputfolder, tmpfn);
                    File.WriteAllBytes(jpeg, images[i]);
                }

            }
        }
        #endregion

        #region Private Methods

        private static List<System.Drawing.Image> ProcessPdfToMemory(Scale scale, List<int> pagenumbers)
        {
            List<System.Drawing.Image> images = new List<System.Drawing.Image>();
            for (int i = 1; i <= Reader.NumberOfPages; i++)
            {
                if ((pagenumbers.Any() && pagenumbers.Contains(i)) || !pagenumbers.Any())
                {
                    Stream stream = ExtractPdfPageStream(i);
                    images.Add(GetPdfImage(((MemoryStream)stream).ToArray(), scale));
                }
            }
            Reader.Close();
            return images;
        }

        private static void ProcessPDF2Filesystem(string outputFolder, Scale scale, CompressionLevel compression, string defaultname = "pdfpic", List<int> pagenumbers=null)
        {
            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
            Encoder myEncoder = Encoder.Quality;
            EncoderParameters myEncoderParameters = new EncoderParameters(1);
            
            EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, GetCompression(compression));
            myEncoderParameters.Param[0] = myEncoderParameter;

            for (int i = 1; i <= Reader.NumberOfPages; i++)
            {
                if (pagenumbers == null || (pagenumbers.Any() && pagenumbers.Contains(i)))
                {
                    Stream stream = ExtractPdfPageStream(i);
                    using (System.Drawing.Image image = GetPdfImage(((MemoryStream)stream).ToArray(), scale))
                    {
                        image.Save($"{outputFolder}\\{defaultname}_{i}.jpg", jpgEncoder, myEncoderParameters);
                    }
                }
            }
            Reader.Close();
        }

        private static long GetCompression(CompressionLevel compression)
        {
            switch (compression)
            {
                case CompressionLevel.High:
                    return 25L;
                case CompressionLevel.Medium:
                    return 50L;
                case CompressionLevel.Low:
                    return 90L;
                case CompressionLevel.None:
                    return 100L;
                default:
                    return 100L;
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private static System.Drawing.Image GetPdfImage(byte[] pdf, Scale resolution)
        {
            var pdfDocument = new PDFiumSharp.PdfDocument(pdf);
            var firstPage = pdfDocument.Pages[0]; //Only one page is expected here
            var pageBitmap = new PDFiumBitmap((int)firstPage.Size.Width * (int)resolution, (int)firstPage.Size.Height * (int)resolution, false);
            pageBitmap.Fill(new PDFiumSharp.Types.FPDF_COLOR(255, 255, 255)); //Lets fill the background with white RGB
            firstPage.Render(pageBitmap);
            System.Drawing.Image image = System.Drawing.Image.FromStream(pageBitmap.AsBmpStream());
            pdfDocument.Close();
            return image;
        }

        private static Stream ExtractPdfPageStream(int pagenumber)
        {
            Stream stream = new MemoryStream();
            Document SourceDocument = new Document(Reader.GetPageSizeWithRotation(pagenumber));
            PdfCopy PdfCopyProvider = new PdfCopy(SourceDocument, stream);
            SourceDocument.Open();
            PdfImportedPage ImportedPage = PdfCopyProvider.GetImportedPage(Reader, pagenumber);
            PdfCopyProvider.AddPage(ImportedPage);
            SourceDocument.Close();
            return stream;
        }
        #endregion
    }
}
