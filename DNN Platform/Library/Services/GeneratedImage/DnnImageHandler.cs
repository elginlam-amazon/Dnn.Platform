// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Options;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.GeneratedImage.FilterTransform;
    using DotNetNuke.Services.GeneratedImage.StartTransform;
    using DotNetNuke.Services.Localization.Internal;

    using Microsoft.Extensions.DependencyInjection;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Bmp;
    using SixLabors.ImageSharp.Formats.Gif;
    using SixLabors.ImageSharp.Formats.Jpeg;
    using SixLabors.ImageSharp.Formats.Png;
    using SixLabors.ImageSharp.PixelFormats;

    using Assembly = System.Reflection.Assembly;

    /// <summary>The endpoint that serves generated images.</summary>
    /// <remarks>
    /// <para>
    /// Was an <c>IHttpHandler</c> registered in <c>web.config</c> as
    /// <c>&lt;add name="DnnImageHandler" path="DnnImageHandler.ashx" verb="*" …/&gt;</c>. Because the
    /// IIS mapping matched the file name at any folder depth (child portals and module folders all
    /// request <c>DnnImageHandler.ashx</c> relative to themselves), the endpoint is mapped both at
    /// the application root and through a wildcard route — see
    /// <c>DnnImageHandlerEndpoint.MapDnnImageHandler</c>. Every existing
    /// <c>DnnImageHandler.ashx?mode=…</c> URL therefore keeps working unchanged.
    /// </para>
    /// <para>Imaging moved from <c>System.Drawing</c> to ImageSharp.</para>
    /// </remarks>
    public class DnnImageHandler : ImageHandler
    {
        /// <summary>
        /// Allow list of server folders where the system allow the dnn image handler to
        /// read to serve image files from it and its subfolders.
        /// </summary>
        private static readonly string[] AllowListFolderPaths =
        {
            Globals.DesktopModulePath,
            Globals.ImagePath,
            Globals.ApplicationPath + "/Portals/",
        };

        private readonly IServiceProvider serviceProvider;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IPortalAliasService portalAliasService;
        private string defaultImageFile = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="DnnImageHandler"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.0. Please use overload with IServiceProvider. Scheduled removal in v12.0.0.")]
        public DnnImageHandler()
            : this(Globals.GetCurrentServiceProvider(), null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnImageHandler"/> class.</summary>
        /// <param name="serviceProvider">The DI container.</param>
        /// <param name="appStatus">The application status.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public DnnImageHandler(IServiceProvider serviceProvider, IApplicationStatusInfo appStatus)
            : this(serviceProvider, appStatus, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnImageHandler"/> class.</summary>
        /// <param name="serviceProvider">The DI container.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalAliasService">The portal alias service.</param>
        public DnnImageHandler(IServiceProvider serviceProvider, IApplicationStatusInfo appStatus, IPortalAliasService portalAliasService)
        {
            this.serviceProvider = serviceProvider;
            this.appStatus = appStatus ?? serviceProvider.GetRequiredService<IApplicationStatusInfo>();
            this.portalAliasService = portalAliasService ?? serviceProvider.GetRequiredService<IPortalAliasService>();

            // Set default settings here
            this.EnableClientCache = true;
            this.EnableServerCache = true;
            this.AllowStandalone = true;
            this.LogSecurity = false;
            this.EnableIPCount = false;
            this.ImageCompression = 95;
            DiskImageStore.PurgeInterval = new TimeSpan(0, 3, 0);
            this.IPCountPurgeInterval = new TimeSpan(0, 5, 0);
            this.IPCountMaxCount = 500;
            this.ClientCacheExpiration = new TimeSpan(0, 10, 0);
            this.AllowedDomains = new[] { string.Empty };

            // read settings from web.config
            this.ReadSettings();
        }

        private Image EmptyImage
        {
            get
            {
                // The GDI+ version made a 1x1 Format1bppIndexed bitmap and called MakeTransparent();
                // a 1x1 fully transparent RGBA pixel is the same thing without the palette dance.
                var emptyBmp = new Image<Rgba32>(1, 1, new Rgba32(0, 0, 0, 0));
                this.ContentType = PngFormat.Instance;

                if (string.IsNullOrEmpty(this.defaultImageFile))
                {
                    return emptyBmp;
                }

                try
                {
                    var fullFilePath = HttpContextSource.MapPath(this.defaultImageFile);

                    if (!File.Exists(fullFilePath) || !IsAllowedFilePathImage(this.defaultImageFile))
                    {
                        return emptyBmp;
                    }

                    var fi = new System.IO.FileInfo(this.defaultImageFile);
                    this.ContentType = GetImageFormat(fi.Extension);

                    using (var stream = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read))
                    {
                        var image = Image.Load(stream);
                        emptyBmp.Dispose();
                        return image;
                    }
                }
                catch (Exception ex)
                {
                    Exceptions.Exceptions.LogException(ex);
                    return emptyBmp;
                }
            }
        }

        // Add image generation logic here and return an instance of ImageInfo

        /// <inheritdoc />
        public override ImageInfo GenerateImage(NameValueCollection parameters)
        {
            SetupCulture();

            // which type of image should be generated ?
            string mode = string.IsNullOrEmpty(parameters["mode"]) ? "profilepic" : parameters["mode"].ToLowerInvariant();

            // We need to determine the output format
            string format = string.IsNullOrEmpty(parameters["format"]) ? "jpg" : parameters["format"].ToLowerInvariant();

            // Lets retrieve the color
            Color color = ParseColor(parameters["color"]);
            Color backColor = ParseColor(parameters["backcolor"]);

            // Do we have a border ?
            int border = string.IsNullOrEmpty(parameters["border"]) ? 0 : Convert.ToInt32(parameters["border"], CultureInfo.InvariantCulture);

            // Do we have a resizemode defined ?
            var resizeMode = string.IsNullOrEmpty(parameters["resizemode"]) ? ImageResizeMode.Fit : (ImageResizeMode)Enum.Parse(typeof(ImageResizeMode), parameters["ResizeMode"], true);

            // Maximum sizes
            int maxWidth = ParseDimension(parameters["MaxWidth"]);
            int maxHeight = ParseDimension(parameters["MaxHeight"]);

            // Any text ?
            string text = string.IsNullOrEmpty(parameters["text"]) ? string.Empty : parameters["text"];

            // Default Image
            this.defaultImageFile = string.IsNullOrEmpty(parameters["NoImage"]) ? string.Empty : parameters["NoImage"];

            // Do we override caching for this image ?
            if (!string.IsNullOrEmpty(parameters["NoCache"]))
            {
                this.EnableClientCache = false;
                this.EnableServerCache = false;
            }

            try
            {
                this.ContentType = GetImageFormat(format);

                switch (mode)
                {
                    case "profilepic":
                        int uid;
                        if (!int.TryParse(parameters["userid"], out uid) || uid <= 0)
                        {
                            uid = -1;
                        }

                        var uppTrans = new UserProfilePicTransform
                        {
                            UserID = uid,
                        };

                        IFileInfo photoFile;
                        this.ContentType = !uppTrans.TryGetPhotoFile(out photoFile)
                            ? GifFormat.Instance
                            : GetImageFormat(photoFile?.Extension ?? "jpg");

                        this.ImageTransforms.Add(uppTrans);
                        break;

                    case "placeholder":
                        var placeHolderTrans = new PlaceholderTransform
                        {
                            Width = ParseDimension(parameters["w"]),
                            Height = ParseDimension(parameters["h"]),
                        };

                        if (!string.IsNullOrEmpty(parameters["Color"]))
                        {
                            placeHolderTrans.Color = color;
                        }

                        if (!string.IsNullOrEmpty(parameters["Text"]))
                        {
                            if (bool.TryParse(Config.GetSetting("AllowDnnImagePlaceholderText"), out var allowDnnImagePlaceholderText) && allowDnnImagePlaceholderText)
                            {
                                placeHolderTrans.Text = text;
                            }
                        }

                        if (!string.IsNullOrEmpty(parameters["BackColor"]))
                        {
                            placeHolderTrans.BackColor = backColor;
                        }

                        this.ImageTransforms.Add(placeHolderTrans);
                        break;

                    case "securefile":
                        var secureFileTrans = new SecureFileTransform();
                        if (!string.IsNullOrEmpty(parameters["FileId"]))
                        {
                            var fileId = Convert.ToInt32(parameters["FileId"], CultureInfo.InvariantCulture);
                            var file = FileManager.Instance.GetFile(fileId);
                            if (file == null)
                            {
                                return this.GetEmptyImageInfo();
                            }

                            var folder = FolderManager.Instance.GetFolder(file.FolderId);
                            if (!secureFileTrans.DoesHaveReadFolderPermission(folder))
                            {
                                return this.GetEmptyImageInfo();
                            }

                            this.ContentType = GetImageFormat(file.Extension);
                            secureFileTrans.SecureFile = file;
                            secureFileTrans.EmptyImage = this.EmptyImage;
                            this.ImageTransforms.Add(secureFileTrans);
                        }

                        break;

                    case "file":
                        var imgFile = string.Empty;
                        var imgUrl = string.Empty;

                        // Lets determine the 2 types of Image Source: Single file, file url
                        var filePath = parameters["File"];
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            filePath = filePath.Trim();
                            var fullFilePath = HttpContextSource.MapPath(filePath);
                            if (!File.Exists(fullFilePath) || !IsAllowedFilePathImage(filePath))
                            {
                                return this.GetEmptyImageInfo();
                            }

                            imgFile = fullFilePath;
                        }
                        else if (!string.IsNullOrEmpty(parameters["Url"]))
                        {
                            var url = parameters["Url"];

                            // allow only site resources when using the url parameter
                            var uriValidator = new UriValidator(this.portalAliasService);
                            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) || !uriValidator.UriBelongsToSite(new Uri(url)))
                            {
                                return this.GetEmptyImageInfo();
                            }

                            imgUrl = url;
                        }

                        if (string.IsNullOrEmpty(parameters["format"]))
                        {
                            string extension;
                            if (string.IsNullOrEmpty(parameters["Url"]))
                            {
                                var fi = new System.IO.FileInfo(imgFile);
                                extension = fi.Extension.ToLowerInvariant();
                            }
                            else
                            {
                                string[] parts = parameters["Url"].Split('.');
                                extension = parts[parts.Length - 1].ToLowerInvariant();
                            }

                            this.ContentType = GetImageFormat(extension);
                        }

                        var imageFileTrans = new ImageFileTransform { ImageFilePath = imgFile, ImageUrl = imgUrl };
                        this.ImageTransforms.Add(imageFileTrans);
                        break;

                    default:
                        var imageHandlerOptions = ConfigurationSource.GetOptions<ImageHandlerOptions>(ImageHandlerOptions.SectionName);
                        if (!imageHandlerOptions.Transforms.TryGetValue(mode, out var imageTransformClass) ||
                            string.IsNullOrEmpty(imageTransformClass))
                        {
                            // Honor the legacy flat "DnnImageHandler.<mode>" application setting too,
                            // so installations that configured a custom transform that way keep working.
                            imageTransformClass = Config.GetSetting("DnnImageHandler." + mode);
                        }

                        string[] imageTransformClassParts = imageTransformClass.Split(',');
                        var assemblyPath = Path.Combine(this.appStatus.ApplicationMapPath, "bin", $"{imageTransformClassParts[1].Trim()}.dll");
                        var asm = Assembly.LoadFrom(assemblyPath);
                        var t = asm.GetType(imageTransformClassParts[0].Trim());
                        var imageTransform = (ImageTransform)ActivatorUtilities.GetServiceOrCreateInstance(this.serviceProvider, t);

                        foreach (var key in parameters.AllKeys)
                        {
                            var pi = t.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (pi != null && key != "mode")
                            {
                                switch (key.ToLowerInvariant())
                                {
                                    case "color":
                                        pi.SetValue(imageTransform, color, null);
                                        break;
                                    case "backcolor":
                                        pi.SetValue(imageTransform, backColor, null);
                                        break;
                                    case "border":
                                        pi.SetValue(imageTransform, border, null);
                                        break;
                                    default:
                                        switch (pi.PropertyType.Name)
                                        {
                                            case "Int32":
                                                pi.SetValue(imageTransform, Convert.ToInt32(parameters[key], CultureInfo.InvariantCulture), null);
                                                break;
                                            case "String":
                                                pi.SetValue(imageTransform, parameters[key], null);
                                                break;
                                        }

                                        break;
                                }
                            }
                        }

                        this.ImageTransforms.Add(imageTransform);
                        break;
                }
            }
            catch (Exception ex)
            {
                Exceptions.Exceptions.LogException(ex);
                return this.GetEmptyImageInfo();
            }

            // Resize-Transformation
            if (mode != "placeholder")
            {
                int width, height;

                width = ParseDimension(parameters["w"]);
                height = ParseDimension(parameters["h"]);

                var size = string.IsNullOrEmpty(parameters["size"]) ? string.Empty : parameters["size"];

                switch (size)
                {
                    case "xxs":
                        width = 16;
                        height = 16;
                        break;
                    case "xs":
                        width = 32;
                        height = 32;
                        break;
                    case "s":
                        width = 50;
                        height = 50;
                        break;
                    case "l":
                        width = 64;
                        height = 64;
                        break;
                    case "xl":
                        width = 128;
                        height = 128;
                        break;
                    case "xxl":
                        width = 256;
                        height = 256;
                        break;
                }

                if (mode == "profilepic")
                {
                    resizeMode = ImageResizeMode.FitSquare;
                    if (width > 0 && height > 0)
                    {
                        maxHeight = height;
                        maxWidth = width;
                        if (width != height)
                        {
                            resizeMode = ImageResizeMode.Fill;
                        }
                    }
                }

                if (width > 0 || height > 0)
                {
                    var resizeTrans = new ImageResizeTransform
                    {
                        Mode = resizeMode,
                        BackColor = backColor,
                        Width = width,
                        Height = height,
                        MaxWidth = maxWidth,
                        MaxHeight = maxHeight,
                        Border = border,
                    };
                    this.ImageTransforms.Add(resizeTrans);
                }
            }

            // Gamma adjustment
            if (!string.IsNullOrEmpty(parameters["Gamma"]))
            {
                var gammaTrans = new ImageGammaTransform();
                double gamma;
                if (double.TryParse(parameters["Gamma"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out gamma) && gamma >= 0.2 && gamma <= 5)
                {
                    gammaTrans.Gamma = gamma;
                    this.ImageTransforms.Add(gammaTrans);
                }
            }

            // Brightness adjustment
            if (!string.IsNullOrEmpty(parameters["Brightness"]))
            {
                var brightnessTrans = new ImageBrightnessTransform();
                int brightness;
                if (int.TryParse(parameters["Brightness"], out brightness))
                {
                    brightnessTrans.Brightness = brightness;
                    this.ImageTransforms.Add(brightnessTrans);
                }
            }

            // Contrast adjustment
            if (!string.IsNullOrEmpty(parameters["Contrast"]))
            {
                var contrastTrans = new ImageContrastTransform();
                double contrast;
                if (double.TryParse(parameters["Contrast"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out contrast) && (contrast >= -100 && contrast <= 100))
                {
                    contrastTrans.Contrast = contrast;
                    this.ImageTransforms.Add(contrastTrans);
                }
            }

            // Greyscale
            if (!string.IsNullOrEmpty(parameters["Greyscale"]))
            {
                var greyscaleTrans = new ImageGreyScaleTransform();
                this.ImageTransforms.Add(greyscaleTrans);
            }

            // Invert
            if (!string.IsNullOrEmpty(parameters["Invert"]))
            {
                var invertTrans = new ImageInvertTransform();
                this.ImageTransforms.Add(invertTrans);
            }

            // Rotate / Flip
            if (!string.IsNullOrEmpty(parameters["RotateFlip"]))
            {
                var rotateFlipTrans = new ImageRotateFlipTransform();
                var rotateFlipType = (RotateFlipType)Enum.Parse(typeof(RotateFlipType), parameters["RotateFlip"]);
                rotateFlipTrans.RotateFlip = rotateFlipType;
                this.ImageTransforms.Add(rotateFlipTrans);
            }

            // We start the chain with an empty image
            using (var emptyImage = new Image<Rgba32>(1, 1))
            using (var ms = new MemoryStream())
            {
                emptyImage.Save(ms, GetEncoder(this.ContentType));
                return new ImageInfo(ms.ToArray());
            }
        }

        /// <summary>
        /// Parses a <c>color</c>/<c>backcolor</c> query-string value. Replaces
        /// <c>ColorTranslator.FromHtml</c> (for <c>#rrggbb</c>) and <c>Color.FromName</c> (for named
        /// colors) with ImageSharp's single parser, which understands both. An unparseable value
        /// falls back to white, as <c>Color.FromName</c>'s unknown-name result effectively did.
        /// </summary>
        private static Color ParseColor(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Color.White;
            }

            return Color.TryParse(value, out var parsed) ? parsed : Color.White;
        }

        /// <summary>Returns the ImageSharp encoder for the requested output format.</summary>
        private static IImageEncoder GetEncoder(IImageFormat format)
        {
            return format switch
            {
                GifFormat => new GifEncoder(),
                BmpFormat => new BmpEncoder(),
                JpegFormat => new JpegEncoder(),
                _ => new PngEncoder(),
            };
        }

        private static bool IsAllowedFilePathImage(string filePath)
        {
            var normalizeFilePath = NormalizeFilePath(filePath.Trim());

            // Resources file cannot be served
            if (filePath.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // File outside the white list cannot be served
            return AllowListFolderPaths.Any(s => normalizeFilePath.StartsWith(s, StringComparison.InvariantCultureIgnoreCase));
        }

        private static string NormalizeFilePath(string filePath)
        {
            var normalizeFilePath = filePath.Replace(@"\", "/");
            if (!normalizeFilePath.StartsWith("/", StringComparison.Ordinal))
            {
                normalizeFilePath = "/" + normalizeFilePath;
            }

            return normalizeFilePath;
        }

        private static int ParseDimension(string value)
        {
            const int DefaultDimension = 0;
            if (string.IsNullOrEmpty(value))
            {
                return DefaultDimension;
            }

            if (!int.TryParse(value, out var dimension))
            {
                if (double.TryParse(value, out var doubleDimension))
                {
                    dimension = (int)Math.Round(doubleDimension, 0);
                }
            }

            // The system won't allow a resize for an image bigger than 4K pixels
            const int MaxDimension = 4000;
            if (dimension is > MaxDimension or < 0)
            {
                dimension = DefaultDimension;
            }

            return dimension;
        }

        /// <summary>
        /// Maps a file extension onto the ImageSharp format to encode the response with. Replaces
        /// the <c>System.Drawing.Imaging.ImageFormat</c> switch.
        /// </summary>
        /// <remarks>
        /// ImageSharp has no ICO encoder, so <c>.ico</c> is served as PNG — which is what GDI+
        /// effectively did too, since it had no ICO encoder either and fell back to PNG bytes.
        /// </remarks>
        private static IImageFormat GetImageFormat(string extension)
        {
            switch (extension.TrimStart('.').ToLowerInvariant())
            {
                case "jpg":
                case "jpeg":
                    return JpegFormat.Instance;
                case "bmp":
                    return BmpFormat.Instance;
                case "gif":
                    return GifFormat.Instance;
                case "png":
                    return PngFormat.Instance;
                case "ico":
                    return PngFormat.Instance;
                default:
                    return PngFormat.Instance;
            }
        }

        private static void SetupCulture()
        {
            var settings = PortalSettings.Current;
            if (settings == null)
            {
                return;
            }

            var pageLocale = TestableLocalization.Instance.GetPageLocale(settings);
            if (pageLocale != null)
            {
                TestableLocalization.Instance.SetThreadCultures(pageLocale, settings);
            }
        }

        private ImageInfo GetEmptyImageInfo()
        {
            return new ImageInfo(this.EmptyImage)
            {
                IsEmptyImage = true,
            };
        }

        private void ReadSettings()
        {
            var settings = ConfigurationSource.GetOptions<ImageHandlerOptions>(ImageHandlerOptions.SectionName).Settings
                           ?? Config.GetSetting("DnnImageHandler");
            if (string.IsNullOrEmpty(settings))
            {
                return;
            }

            string[] values = settings.Split(';');
            foreach (string value in values)
            {
                string[] setting = value.Split('=');
                string name = setting[0].ToLowerInvariant();
                switch (name)
                {
                    case "enableclientcache":
                        this.EnableClientCache = Convert.ToBoolean(setting[1], CultureInfo.InvariantCulture);
                        break;
                    case "clientcacheexpiration":
                        this.ClientCacheExpiration = TimeSpan.FromSeconds(Convert.ToInt32(setting[1], CultureInfo.InvariantCulture));
                        break;
                    case "enableservercache":
                        this.EnableServerCache = Convert.ToBoolean(setting[1], CultureInfo.InvariantCulture);
                        break;
                    case "servercacheexpiration":
                        DiskImageStore.PurgeInterval = TimeSpan.FromSeconds(Convert.ToInt32(setting[1], CultureInfo.InvariantCulture));
                        break;
                    case "allowstandalone":
                        this.AllowStandalone = Convert.ToBoolean(setting[1], CultureInfo.InvariantCulture);
                        break;
                    case "logsecurity":
                        this.LogSecurity = Convert.ToBoolean(setting[1], CultureInfo.InvariantCulture);
                        break;
                    case "imagecompression":
                        this.ImageCompression = Convert.ToInt32(setting[1], CultureInfo.InvariantCulture);
                        break;
                    case "alloweddomains":
                        this.AllowedDomains = setting[1].Split(',');
                        break;
                    case "enableipcount":
                        this.EnableIPCount = Convert.ToBoolean(setting[1], CultureInfo.InvariantCulture);
                        break;
                    case "ipcountmax":
                        this.IPCountMaxCount = Convert.ToInt32(setting[1], CultureInfo.InvariantCulture);
                        break;
                    case "ipcountpurgeinterval":
                        this.IPCountPurgeInterval = TimeSpan.FromSeconds(Convert.ToInt32(setting[1], CultureInfo.InvariantCulture));
                        break;
                }
            }
        }
    }
}
