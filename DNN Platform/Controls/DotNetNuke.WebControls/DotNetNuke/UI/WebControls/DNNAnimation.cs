// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.ComponentModel;
    using System.Globalization;
    using System.Text;

    using DotNetNuke.UI.Utilities;
    using DotNetNuke.UI.Utilities.Animation;

    /// <summary>
    /// The animation applied to showing and hiding the sub-menus of a <see cref="DNNMenu"/> and the
    /// pop-up parts of the Blazor components of this library.
    /// </summary>
    /// <remarks>
    /// The original implemented <c>System.Web.UI.IStateManager</c> and backed every property with a
    /// <c>StateBag</c>. Neither has a .NET 10 equivalent, so the properties are now ordinary
    /// backing fields initialized to the very same defaults the state-bag getters returned, and the
    /// state-manager members are gone (they were pure persistence plumbing with no behavior).
    /// <para>
    /// The animation itself is no longer performed by the <c>dnn.motion</c> script: this type is
    /// now also the descriptor of the equivalent CSS transition, which the components render into
    /// the <c>style</c> attribute of the animated element (see <see cref="ToCssTransition"/>).
    /// </para>
    /// </remarks>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class DNNAnimation : IUrlResolutionService
    {
        private AnimationType animationType = AnimationType.None;
        private EasingDirection easingDirection = EasingDirection.In;
        private EasingType easingType = EasingType.Expo;
        private int length = 1;
        private int interval = 10;

        /// <summary>Gets or sets the type of the animation.</summary>
        [NotifyParentProperty(true)]
        [DefaultValue(0)]
        [ClientProperty]
        [ClientPropertyName("anim")]
        public AnimationType AnimationType
        {
            get
            {
                return !ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.Motion)
                    ? AnimationType.None
                    : this.animationType;
            }

            set
            {
                this.animationType = value;
            }
        }

        /// <summary>Gets or sets the direction the easing is applied in.</summary>
        [ClientPropertyName("easeDir")]
        [DefaultValue(1)]
        [ClientProperty]
        [NotifyParentProperty(true)]
        public EasingDirection EasingDirection
        {
            get { return this.easingDirection; }
            set { this.easingDirection = value; }
        }

        /// <summary>Gets or sets the easing function of the animation.</summary>
        [DefaultValue(3)]
        [NotifyParentProperty(true)]
        [ClientProperty]
        [ClientPropertyName("easeType")]
        public EasingType EasingType
        {
            get { return this.easingType; }
            set { this.easingType = value; }
        }

        /// <summary>Gets or sets the length of the animation.</summary>
        [DefaultValue(1)]
        [NotifyParentProperty(true)]
        [ClientProperty]
        [ClientPropertyName("animLen")]
        public int Length
        {
            get { return this.length; }
            set { this.length = value; }
        }

        /// <summary>Gets or sets the interval between the frames of the animation.</summary>
        [ClientProperty]
        [NotifyParentProperty(true)]
        [DefaultValue(10)]
        [ClientPropertyName("animInt")]
        public int Interval
        {
            get { return this.interval; }
            set { this.interval = value; }
        }

        /// <summary>Gets the duration of the animation, in milliseconds.</summary>
        /// <remarks>
        /// The client script stepped <see cref="Length"/> times every <see cref="Interval"/>
        /// milliseconds, so the wall-clock duration of the animation is their product.
        /// </remarks>
        public int DurationMilliseconds
        {
            get { return this.Length * this.Interval; }
        }

        /// <summary>Gets the CSS timing function matching the configured easing.</summary>
        /// <remarks>
        /// REVIEW: behavior inferred from usage — verify. The <c>dnn.motion</c> easing curves are
        /// expressed as the cubic Bézier curves of the same names, in the configured direction.
        /// </remarks>
        public string TimingFunction
        {
            get
            {
                var curve = this.EasingType switch
                {
                    EasingType.Bounce => (0.68, -0.55, 0.265, 1.55),
                    EasingType.Circ => (0.075, 0.82, 0.165, 1.0),
                    EasingType.Cubic => (0.215, 0.61, 0.355, 1.0),
                    EasingType.Expo => (0.19, 1.0, 0.22, 1.0),
                    EasingType.Quad => (0.25, 0.46, 0.45, 0.94),
                    EasingType.Quint => (0.23, 1.0, 0.32, 1.0),
                    EasingType.Quart => (0.165, 0.84, 0.44, 1.0),
                    EasingType.Sine => (0.39, 0.575, 0.565, 1.0),
                    _ => (0.25, 0.1, 0.25, 1.0),
                };

                var (x1, y1, x2, y2) = curve;
                if (this.EasingDirection == EasingDirection.Out)
                {
                    // The out direction is the in curve mirrored through its midpoint.
                    (x1, y1, x2, y2) = (1 - x2, 1 - y2, 1 - x1, 1 - y1);
                }
                else if (this.EasingDirection == EasingDirection.InOut)
                {
                    // The symmetric curve of the in/out direction, as dnn.motion's easeInOut.
                    y1 = 0.0;
                    x2 = 1 - x1;
                    y2 = 1.0;
                }

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "cubic-bezier({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})",
                    x1,
                    y1,
                    x2,
                    y2);
            }
        }

        /// <summary>Gets the CSS properties the animation transitions.</summary>
        /// <remarks>
        /// Mirrors what each <see cref="AnimationType"/> of the client script moved: a slide
        /// changed the height, an expand the width, and the two diagonal types both.
        /// </remarks>
        public string TransitionProperties
        {
            get
            {
                return this.AnimationType switch
                {
                    AnimationType.Slide => "height",
                    AnimationType.Expand => "width",
                    AnimationType.Diagonal => "width, height",
                    AnimationType.ReverseDiagonal => "width, height",
                    _ => string.Empty,
                };
            }
        }

        /// <inheritdoc/>
        public string ResolveClientUrl(string url)
        {
            return url;
        }

        /// <summary>
        /// Gets the CSS declaration that performs this animation in the browser.
        /// </summary>
        /// <returns>
        /// The <c>transition</c> declaration, or an empty string when
        /// <see cref="AnimationType.None"/> is configured.
        /// </returns>
        /// <remarks>
        /// Replaces the <c>dnn.motion</c> script the WebForms controls drove through their
        /// marshalled <c>anim</c>/<c>animLen</c>/<c>animInt</c>/<c>easeType</c>/<c>easeDir</c>
        /// properties: the Blazor components render this declaration into the <c>style</c>
        /// attribute of the element being shown or hidden.
        /// </remarks>
        public string ToCssTransition()
        {
            var properties = this.TransitionProperties;
            if (this.AnimationType == AnimationType.None || properties.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var names = properties.Split(',');
            for (var index = 0; index < names.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(
                    CultureInfo.InvariantCulture,
                    $"{names[index].Trim()} {this.DurationMilliseconds}ms {this.TimingFunction}");
            }

            return "transition: " + builder.ToString() + ";";
        }
    }
}
