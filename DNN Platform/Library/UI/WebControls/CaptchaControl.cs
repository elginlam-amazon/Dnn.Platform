// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using System.Text.RegularExpressions;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security.Authentication;
    using DotNetNuke.Services.Cache;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.Logging;

    using SixLabors.Fonts;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Drawing;
    using SixLabors.ImageSharp.Drawing.Processing;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;

    using Image = SixLabors.ImageSharp.Image;

    /// <summary>The CaptchaControl control provides a Captcha Challenge control.</summary>
    /// <remarks>
    /// Was a <c>WebControl</c> implementing <c>IPostBackDataHandler</c> that rendered itself onto an
    /// <c>HtmlTextWriter</c> and drew its image with <c>System.Drawing</c>. It is now a
    /// <see cref="DnnComponentBase"/> that renders the same markup through the render tree and draws
    /// with SixLabors.ImageSharp. The challenge text, which used to travel in encrypted view state,
    /// lives in a field now (view state is gone); it is still the cache entry keyed by
    /// <see cref="DataCache.CaptchaCacheKey"/> that decides whether an answer is valid, and the
    /// image URL still carries the challenge in a data-protected ticket.
    /// </remarks>
    public partial class CaptchaControl : DnnComponentBase
    {
        internal const string KEY = "captcha";
        private const int EXPIRATIONDEFAULT = 120;
        private const int LENGTHDEFAULT = 6;
        private const string RENDERURLDEFAULT = "ImageChallenge.captcha.aspx";
        private const string CHARSDEFAULT = "abcdefghijklmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<CaptchaControl>();
        private static readonly string[] FontFamilies =
        {
            "Comic Sans MS",
            "Consolas",
            "Courier New",
            "Franklin Gothic Medium",
            "Georgia",
            "Impact",
            "Lucida Console",
            "MS Sans Serif",
            "Trebuchet MS",
        };

        private static readonly Random Rand = new Random();
        private static string separator = ":-:";
        private readonly Style errorStyle = new Style();
        private readonly Style textBoxStyle = new Style();
        private Color backGroundColor = Color.Transparent;
        private string backGroundImage = string.Empty;
        private string captchaChars = CHARSDEFAULT;
        private Unit captchaHeight = Unit.Pixel(100);
        private int captchaLength = LENGTHDEFAULT;
        private string captchaText;
        private Unit captchaWidth = Unit.Pixel(300);
        private int expiration;
        private bool isValid;
        private string renderUrl = RENDERURLDEFAULT;
        private string userText = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="CaptchaControl"/> class.</summary>
        public CaptchaControl()
        {
            this.ErrorMessage = Localization.GetString("InvalidCaptcha", Localization.SharedResourceFile);
            this.Text = Localization.GetString("CaptchaText.Text", Localization.SharedResourceFile);
            this.expiration = HostController.Instance.GetInteger("EXPIRATION_DEFAULT", EXPIRATIONDEFAULT);
        }

        /// <summary>
        /// Occurs when the user has validated the captcha.
        /// </summary>
        public event ServerValidateEventHandler UserValidated;

        /// <summary>Gets the Style to use for the Error Message.</summary>
        [Browsable(true)]
        [Category("Appearance")]
        [Description("Set the Style for the Error Message Control.")]
        public Style ErrorStyle => this.errorStyle;

        /// <summary>Gets a value indicating whether the control is valid.</summary>
        [Category("Validation")]
        [Description("Returns True if the user was CAPTCHA validated after a postback.")]
        public bool IsValid => this.isValid;

        /// <summary>Gets the Style to use for the Text Box.</summary>
        [Browsable(true)]
        [Category("Appearance")]
        [Description("Set the Style for the Text Box Control.")]
        public Style TextBoxStyle => this.textBoxStyle;

        /// <summary>Gets or sets the BackGroundColor.</summary>
        /// <remarks>Was a <c>System.Drawing.Color</c>; it is a <see cref="SixLabors.ImageSharp.Color"/> now.</remarks>
        [Category("Appearance")]
        [Description("The Background Color to use for the Captcha Image.")]
        [Parameter]
        public Color BackGroundColor
        {
            get => this.backGroundColor;
            set => this.backGroundColor = value;
        }

        /// <summary>Gets or sets the BackGround Image.</summary>
        [Category("Appearance")]
        [Description("A Background Image to use for the Captcha Image.")]
        [Parameter]
        public string BackGroundImage
        {
            get => this.backGroundImage;
            set => this.backGroundImage = value;
        }

        /// <summary>Gets or sets the list of characters.</summary>
        [Category("Behavior")]
        [DefaultValue(CHARSDEFAULT)]
        [Description("Characters used to render CAPTCHA text. A character will be picked randomly from the string.")]
        [Parameter]
        public string CaptchaChars
        {
            get => this.captchaChars;
            set => this.captchaChars = value;
        }

        /// <summary>Gets or sets the height of the Captcha image.</summary>
        [Category("Appearance")]
        [Description("Height of Captcha Image.")]
        [Parameter]
        public Unit CaptchaHeight
        {
            get => this.captchaHeight;
            set => this.captchaHeight = value;
        }

        /// <summary>Gets or sets the length of the Captcha string.</summary>
        [Category("Behavior")]
        [DefaultValue(LENGTHDEFAULT)]
        [Description("Number of CaptchaChars used in the CAPTCHA text")]
        [Parameter]
        public int CaptchaLength
        {
            get => this.captchaLength;
            set => this.captchaLength = value;
        }

        /// <summary>Gets or sets the width of the Captcha image.</summary>
        [Category("Appearance")]
        [Description("Width of Captcha Image.")]
        [Parameter]
        public Unit CaptchaWidth
        {
            get => this.captchaWidth;
            set => this.captchaWidth = value;
        }

        /// <summary>Gets or sets the ErrorMessage to display if the control is invalid.</summary>
        [Category("Behavior")]
        [Description("The Error Message to display if invalid.")]
        [DefaultValue("")]
        [Parameter]
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets the Expiration time in seconds.</summary>
        [Category("Behavior")]
        [Description("The duration of time (seconds) a user has before the challenge expires.")]
        [DefaultValue(EXPIRATIONDEFAULT)]
        [Parameter]
        public int Expiration
        {
            get => this.expiration;
            set => this.expiration = value;
        }

        /// <summary>Gets or sets the Url to use to render the control.</summary>
        [Category("Behavior")]
        [Description("The URL used to render the image to the client.")]
        [DefaultValue(RENDERURLDEFAULT)]
        [Parameter]
        public string RenderUrl
        {
            get => this.renderUrl;
            set => this.renderUrl = value;
        }

        /// <summary>Gets or sets the Help Text to use.</summary>
        [Category("Captcha")]
        [DefaultValue("Enter the code shown above:")]
        [Description("Instructional text displayed next to CAPTCHA image.")]
        [Parameter]
        public string Text { get; set; }

        /// <summary>Gets or sets the tool tip of the image.</summary>
        /// <remarks>Replaces <c>WebControl.ToolTip</c>.</remarks>
        [Parameter]
        public string ToolTip { get; set; }

        /// <summary>Gets or sets the width of the answer text box.</summary>
        /// <remarks>Replaces <c>WebControl.Width</c>.</remarks>
        [Parameter]
        public Unit Width { get; set; }

        /// <summary>Gets or sets a value indicating whether the answer text box is enabled.</summary>
        /// <remarks>Replaces <c>WebControl.Enabled</c>.</remarks>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets the access key of the answer text box.</summary>
        /// <remarks>Replaces <c>WebControl.AccessKey</c>.</remarks>
        [Parameter]
        public string AccessKey { get; set; }

        /// <summary>Gets or sets the tab index of the answer text box.</summary>
        /// <remarks>Replaces <c>WebControl.TabIndex</c>.</remarks>
        [Parameter]
        public short TabIndex { get; set; }

        /// <inheritdoc />
        protected override string TagName => null;

        /// <summary>LoadPostData loads the Post Back Data and determines whether the value has change.</summary>
        /// <param name="postDataKey">A key to the PostBack Data to load.</param>
        /// <param name="postCollection">A name value collection of postback data.</param>
        /// <returns>Always <see langword="false"/>.</returns>
        public virtual bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            this.userText = postCollection[postDataKey];
            this.Validate(this.userText);
            if (!this.isValid && !string.IsNullOrEmpty(this.userText))
            {
                this.captchaText = this.GetNextCaptcha();
            }

            return false;
        }

        /// <summary>RaisePostDataChangedEvent runs when the PostBackData has changed.</summary>
        public void RaisePostDataChangedEvent()
        {
        }

        /// <summary>Validates the posted back data.</summary>
        /// <param name="userData">The user entered data.</param>
        /// <returns><see langword="true"/> if the data is valid, otherwise <see langword="false"/>.</returns>
        public bool Validate(string userData)
        {
            var cacheKey = string.Format(CultureInfo.InvariantCulture, DataCache.CaptchaCacheKey, userData);
            var cacheObj = DataCache.GetCache(cacheKey);

            if (cacheObj == null)
            {
                this.isValid = false;
            }
            else
            {
                this.isValid = true;
                DataCache.RemoveCache(cacheKey);
            }

            this.OnUserValidated(new ServerValidateEventArgs(this.captchaText, this.isValid));
            return this.isValid;
        }

        /// <summary>GenerateImage generates the Captcha Image.</summary>
        /// <param name="encryptedText">The Encrypted Text to display.</param>
        /// <returns>The image, or <see langword="null"/> when the ticket could not be read.</returns>
        /// <remarks>Was a <c>System.Drawing.Bitmap</c>; it is an ImageSharp <see cref="Image"/> now.</remarks>
        internal static Image GenerateImage(string encryptedText)
        {
            string encodedText = Decrypt(encryptedText);
            Image bmp = null;
            string[] settings = Regex.Split(encodedText, separator);
            try
            {
                int width;
                int height;
                if (settings.Length > 3 && int.TryParse(settings[0], out width) && int.TryParse(settings[1], out height))
                {
                    string text = settings[2];
                    string backgroundImage = settings[3];

                    // the generated background is light gray text, a supplied one gets black text
                    var brush = string.IsNullOrEmpty(backgroundImage)
                        ? Brushes.Solid(Color.LightGray)
                        : Brushes.Solid(Color.Black);

                    bmp = string.IsNullOrEmpty(backgroundImage)
                        ? CreateImage(width, height)
                        : Image.Load(HttpContextSource.MapPath(backgroundImage));

                    DrawText(bmp, text, width, height, brush);
                }
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
                bmp?.Dispose();
                bmp = null;
            }

            return bmp;
        }

        /// <summary>Gets the next Captcha.</summary>
        /// <returns>The challenge string.</returns>
        protected virtual string GetNextCaptcha()
        {
            var sb = new StringBuilder();
            var rand = new Random();
            int n;
            var intMaxLength = this.CaptchaChars.Length;

            for (n = 0; n <= this.CaptchaLength - 1; n++)
            {
                sb.Append(this.CaptchaChars.Substring(rand.Next(intMaxLength), 1));
            }

            var challenge = sb.ToString();

            // NOTE: this could be a problem in a web farm using in-memory caching where
            // the request might go to another server in the farm. Also, in a system
            // with a single server or web-farm, the cache might be cleared
            // which will cause a problem in such case unless sticky sessions are used.
            var cacheKey = string.Format(CultureInfo.InvariantCulture, DataCache.CaptchaCacheKey, challenge);
            DataCache.SetCache(
                cacheKey,
                challenge,
                (DNNCacheDependency)null,
                DateTime.Now.AddSeconds(this.expiration + 1),
                Cache.NoSlidingExpiration,
                CacheItemPriority.AboveNormal,
                null);
            return challenge;
        }

        /// <inheritdoc />
        /// <remarks>
        /// This is the body of the WebForms <c>OnPreRender</c> override, minus
        /// <c>Page.RegisterRequiresViewStateEncryption()</c>: there is no view state to encrypt any
        /// more, and the challenge never reaches the client in the clear — it travels in the
        /// data-protected ticket on the image URL, see <see cref="GetUrl"/>.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (this.CaptchaWidth.IsEmpty || this.CaptchaWidth.Type != UnitType.Pixel || this.CaptchaHeight.IsEmpty || this.CaptchaHeight.Type != UnitType.Pixel)
            {
                throw new InvalidOperationException("Must specify size of control in pixels.");
            }

            // Generate Random Challenge Text
            this.captchaText = this.GetNextCaptcha();
        }

        /// <summary>
        /// Raises the <see cref="UserValidated" /> event.
        /// </summary>
        /// <param name="e">The <see cref="ServerValidateEventArgs"/> instance containing the event data.</param>
        protected virtual void OnUserValidated(ServerValidateEventArgs e)
        {
            ServerValidateEventHandler handler = this.UserValidated;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (string.IsNullOrEmpty(this.captchaText))
            {
                this.captchaText = this.GetNextCaptcha();
            }

            // Render outer <div> Tag
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "dnnLeft");

            // Render image <img> Tag
            builder.OpenElement(sequence++, "img");
            builder.AddAttribute(sequence++, "src", this.GetUrl());
            builder.AddAttribute(sequence++, "border", "0");
            builder.AddAttribute(
                sequence++,
                "alt",
                string.IsNullOrEmpty(this.ToolTip)
                    ? Localization.GetString("CaptchaAlt.Text", Localization.SharedResourceFile)
                    : this.ToolTip);
            builder.CloseElement();

            // Render Help Text
            if (!string.IsNullOrEmpty(this.Text))
            {
                builder.OpenElement(sequence++, "div");
                builder.AddMarkupContent(sequence++, this.Text);
                builder.CloseElement();
            }

            // Render text box <input> Tag
            builder.OpenElement(sequence++, "input");
            this.TextBoxStyle.AddAttributesToRender(builder, sequence);
            sequence += 2;
            builder.AddAttribute(sequence++, "type", "text");
            builder.AddAttribute(sequence++, "style", "width:" + this.Width);
            builder.AddAttribute(sequence++, "maxlength", this.captchaText.Length.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(sequence++, "name", this.UniqueId);
            if (!string.IsNullOrEmpty(this.AccessKey))
            {
                builder.AddAttribute(sequence++, "accesskey", this.AccessKey);
            }

            if (!this.Enabled)
            {
                builder.AddAttribute(sequence++, "disabled", "disabled");
            }

            if (this.TabIndex > 0)
            {
                builder.AddAttribute(sequence++, "tabindex", this.TabIndex.ToString(CultureInfo.InvariantCulture));
            }

            builder.AddAttribute(sequence++, "value", this.userText == this.captchaText ? this.userText : string.Empty);
            builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.AnswerChanged));
            builder.CloseElement();

            // Render error message
            if (!this.IsValid && !string.IsNullOrEmpty(this.userText))
            {
                builder.OpenElement(sequence++, "span");
                this.ErrorStyle.AddAttributesToRender(builder, sequence);
                sequence += 2;
                builder.AddMarkupContent(sequence++, this.ErrorMessage);
                builder.CloseElement();
            }

            // Render </div>
            builder.CloseElement();

            base.RenderContents(builder, ref sequence);
        }

        /// <summary>Creates the Image.</summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        private static Image<Rgba32> CreateImage(int width, int height)
        {
            var bmp = new Image<Rgba32>(width, height);
            var rect = new RectangularPolygon(0, 0, width, height);

            var angle = Convert.ToSingle(Rand.NextDouble()) * 360f;
            var radians = angle * (float)Math.PI / 180f;
            var start = new PointF(0, 0);
            var end = new PointF(
                width * (float)Math.Cos(radians),
                height * (float)Math.Sin(radians));
            var brush = new LinearGradientBrush(
                start,
                end,
                GradientRepetitionMode.None,
                new ColorStop(0f, Color.FromRgb((byte)Rand.Next(224), (byte)Rand.Next(224), (byte)Rand.Next(224))),
                new ColorStop(1f, Color.FromRgb((byte)Rand.Next(224), (byte)Rand.Next(224), (byte)Rand.Next(224))));

            bmp.Mutate(ctx => ctx.Fill(brush, rect));

            AddNoise(bmp, width, height);

            if (Rand.Next(2) == 1)
            {
                DistortImage(bmp, Rand.Next(5, 20));
            }
            else
            {
                DistortImage(bmp, -Rand.Next(5, 20));
            }

            return bmp;
        }

        private static void AddNoise(Image image, int width, int height)
        {
            Random rand = new Random();
            int numDots = rand.Next(width * height / 50, width * height / 25);

            image.Mutate(ctx =>
            {
                for (int i = 0; i < numDots; i++)
                {
                    int x = rand.Next(0, width);
                    int y = rand.Next(0, height);

                    // Choose random brightness for noise
                    int brightness = rand.Next(40, 240);
                    var color = Color.FromRgba((byte)brightness, (byte)brightness, (byte)brightness, (byte)rand.Next(40, 100));

                    ctx.Fill(color, new RectangularPolygon(x, y, 1, 1)); // Draw tiny noise dot
                }
            });
        }

        /// <summary>Draws the Text onto the image.</summary>
        /// <param name="image">The image to draw onto.</param>
        /// <param name="text">The text to display.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="brush">The brush the glyphs are filled with.</param>
        /// <remarks>
        /// Replaces <c>CreateText</c>, which built a <c>GraphicsPath</c> the caller then filled. With
        /// ImageSharp the glyphs are drawn straight onto the image, so the per-character jitter and
        /// rotation are applied through the drawing transform. The perspective warp the original
        /// applied to the whole text path (<c>GraphicsPath.Warp</c>) has no ImageSharp equivalent;
        /// the per-character rotation, the jitter, the noise and the random lines all remain.
        /// </remarks>
        private static void DrawText(Image image, string text, int width, int height, Brush brush)
        {
            var maxFontSize = height * 1f;
            var minFontSize = height * 0.8f;
            float leftMargin = 10;
            float rightMargin = 10;
            float availableWidth = width - leftMargin - rightMargin;
            float xOffset = leftMargin;
            float charSpacing = availableWidth / text.Length;
            Random rand = new Random();

            try
            {
                foreach (char c in text)
                {
                    var ff = GetFont(); // Get a random font for each character
                    var emSize = maxFontSize;

                    Font f;
                    FontRectangle charSize;

                    // Find the largest font size that fits within the available space
                    do
                    {
                        f = ff.CreateFont(emSize, FontStyle.Bold);
                        charSize = TextMeasurer.MeasureSize(c.ToString(), new TextOptions(f));
                        emSize -= 1;
                    }
                    while ((charSize.Width > charSpacing || charSize.Height > height) && emSize > minFontSize);

                    // Ensure the character doesn't exceed the available width
                    float jitter = RandomJitter(-3, 3, rand);
                    if (xOffset + charSize.Width + jitter > width - rightMargin)
                    {
                        jitter = -Math.Abs(jitter); // Adjust jitter to avoid overflow
                    }

                    // Calculate position (centered vertically, random X jitter)
                    float yOffset = ((height - charSize.Height) / 2) + RandomJitter(-5, 5, rand);
                    float charX = xOffset + jitter;

                    // Generate a random rotation angle (-15° to 15°)
                    float rotationAngle = RandomJitter(-15, 15, rand);
                    float charCenterX = charX + (charSize.Width / 2);
                    float charCenterY = yOffset + (charSize.Height / 2);
                    var transform = Matrix3x2.CreateRotation(
                        rotationAngle * (float)Math.PI / 180f,
                        new Vector2(charCenterX, charCenterY));

                    var glyph = c.ToString();
                    var font = f;
                    image.Mutate(ctx => ctx.DrawText(
                        new DrawingOptions { Transform = transform },
                        glyph,
                        font,
                        brush,
                        new PointF(charX, yOffset)));

                    // Move X position for the next character
                    xOffset += charSpacing;
                }

                DrawRandomLines(image, width, height, rand);
            }
            catch (Exception exc)
            {
                Logger.CaptchaControlCreateTextException(exc);
            }
        }

        private static void DrawRandomLines(Image image, int width, int height, Random rand)
        {
            int numLines = rand.Next(3, 7);
            image.Mutate(ctx =>
            {
                for (int i = 0; i < numLines; i++)
                {
                    int x1 = rand.Next(0, width);
                    int y1 = rand.Next(0, height);
                    int x2 = rand.Next(0, width);
                    int y2 = rand.Next(0, height);

                    // Generate a color with random transparency
                    var color = Color.FromRgba(
                        (byte)rand.Next(150, 200), // Red
                        (byte)rand.Next(150, 200), // Green
                        (byte)rand.Next(150, 200), // Blue
                        (byte)rand.Next(100, 200)); // Opacity

                    ctx.DrawLine(color, rand.Next(1, 3), new PointF(x1, y1), new PointF(x2, y2));
                }
            });
        }

        private static float RandomJitter(float min, float max, Random rand)
        {
            return (float)((rand.NextDouble() * (max - min)) + min);
        }

        /// <summary>Decrypts the CAPTCHA Text.</summary>
        /// <param name="encryptedContent">The encrypted text.</param>
        /// <remarks>
        /// Replaces <c>FormsAuthentication.Decrypt</c> with the platform's data-protected ticket.
        /// </remarks>
        private static string Decrypt(string encryptedContent)
        {
            string decryptedText = string.Empty;
            try
            {
                DnnAuthenticationTicket ticket = AuthenticationCookieService.Current.Unprotect(encryptedContent);
                if (ticket != null && !ticket.Expired)
                {
                    decryptedText = ticket.UserData;
                }
            }
            catch (ArgumentException exc)
            {
                Logger.CaptchaControlDecryptException(exc);
            }

            return decryptedText;
        }

        /// <summary>DistortImage distorts the captcha image.</summary>
        /// <param name="b">The Image to distort.</param>
        /// <param name="distortion">Distortion.</param>
        private static void DistortImage(Image<Rgba32> b, double distortion)
        {
            int width = b.Width;
            int height = b.Height;

            using (var copy = b.Clone())
            {
                for (int y = 0; y <= height - 1; y++)
                {
                    for (int x = 0; x <= width - 1; x++)
                    {
                        int newX = Convert.ToInt32(x + (distortion * Math.Sin(Math.PI * y / 64.0)));
                        int newY = Convert.ToInt32(y + (distortion * Math.Cos(Math.PI * x / 64.0)));
                        if (newX < 0 || newX >= width)
                        {
                            newX = 0;
                        }

                        if (newY < 0 || newY >= height)
                        {
                            newY = 0;
                        }

                        b[x, y] = copy[newX, newY];
                    }
                }
            }
        }

        /// <summary>Encrypts the CAPTCHA Text.</summary>
        /// <param name="content">The text to encrypt.</param>
        /// <param name="expiration">The time the ticket expires.</param>
        /// <remarks>
        /// Replaces <c>FormsAuthentication.Encrypt</c> with the platform's data-protected ticket.
        /// </remarks>
        private static string Encrypt(string content, DateTime expiration)
        {
            var request = HttpContextSource.Current?.Request;
            var ticket = new DnnAuthenticationTicket
            {
                Version = 1,
                Name = request?.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty,
                IssueDate = DateTime.Now,
                Expiration = expiration,
                IsPersistent = false,
                UserData = content,
            };

            return AuthenticationCookieService.Current.Protect(ticket);
        }

        /// <summary>GetFont gets a random font to use for the Captcha Text.</summary>
        /// <remarks>
        /// Replaces <c>new System.Drawing.FontFamily(name)</c> with the ImageSharp font collection.
        /// The original list of families is tried first; when none of them is installed any
        /// installed family is used, because a captcha without text would let every bot through.
        /// </remarks>
        private static FontFamily GetFont()
        {
            var installed = SystemFonts.Families.ToList();
            if (installed.Count == 0)
            {
                throw new InvalidOperationException(
                    "No font is installed on this machine, so the CAPTCHA image cannot be drawn. Install a font package (for example 'dejavu-fonts' or 'mscorefonts') on the server.");
            }

            var candidates = FontFamilies
                .Where(name => SystemFonts.Collection.TryGet(name, out _))
                .ToList();
            if (candidates.Count > 0)
            {
                return SystemFonts.Get(candidates[Rand.Next(candidates.Count)]);
            }

            Logger.LogWarning(
                "None of the CAPTCHA font families is installed; falling back to an installed font. Install a font package providing one of: {Families}.",
                string.Join(", ", FontFamilies));
            return installed[Rand.Next(installed.Count)];
        }

        private void AnswerChanged(ChangeEventArgs args)
        {
            this.userText = args?.Value?.ToString() ?? string.Empty;
            this.Validate(this.userText);
            if (!this.isValid && !string.IsNullOrEmpty(this.userText))
            {
                this.captchaText = this.GetNextCaptcha();
            }
        }

        /// <summary>Builds the URL for the Handler.</summary>
        private string GetUrl()
        {
            var url = Globals.ResolveUrl(this.RenderUrl);
            var encryptedTicket = Encrypt(this.EncodeTicket(), DateTime.Now.AddSeconds(this.Expiration));
            url += $"?{KEY}={Uri.EscapeDataString(encryptedTicket)}";

            // Append the Alias to the url so that it doesn't lose track of the alias it's currently on
            var portalSettings = PortalSettings.Current;
            url += "&alias=" + ((IPortalAliasInfo)portalSettings.PortalAlias).HttpAlias;
            return url;
        }

        /// <summary>Encodes the querystring to pass to the Handler.</summary>
        private string EncodeTicket()
        {
            var sb = new StringBuilder();

            sb.Append(this.CaptchaWidth.Value);
            sb.Append(separator + this.CaptchaHeight.Value);
            sb.Append(separator + this.captchaText);
            sb.Append(separator + this.BackGroundImage);

            return sb.ToString();
        }
    }
}
