using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web;
using System.Web.Mvc;

namespace GTX
{
    internal static class RichContentImageUpload
    {
        internal const string DirectoryPath = "~/App_Data/SpecialsImages";

        internal static ActionResult Save(HttpPostedFileBase file, HttpServerUtilityBase server, UrlHelper url)
        {
            if (file == null || file.ContentLength == 0 || file.ContentLength > 5 * 1024 * 1024)
                return new HttpStatusCodeResult(400, "Choose an image up to 5 MB.");

            try
            {
                using (var source = Image.FromStream(file.InputStream, false, true))
                {
                    if (source.Width > 6000 || source.Height > 6000 || (long)source.Width * source.Height > 20000000)
                        return new HttpStatusCodeResult(400, "Image dimensions are too large.");

                    // Re-encode decoded pixels. The filename comes from the server.
                    var directory = server.MapPath(DirectoryPath);
                    Directory.CreateDirectory(directory);
                    var name = Guid.NewGuid().ToString("N");
                    using (var bitmap = new Bitmap(source))
                    {
                        bitmap.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
                    }

                    return new JsonResult { Data = new { url = url.Action("Image", "Specials", new { id = name }) } };
                }
            }
            catch (ArgumentException)
            {
                return new HttpStatusCodeResult(400, "Choose a valid PNG, JPEG, GIF or BMP image.");
            }
            catch (OutOfMemoryException)
            {
                return new HttpStatusCodeResult(400, "The image could not be decoded.");
            }
        }
    }
}
