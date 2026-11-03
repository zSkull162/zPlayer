using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace zPlayer.App
{
    public class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);

        public GridLength From { get; set; }
        public GridLength To { get; set; }
        public IEasingFunction? EasingFunction { get; set; }

        protected override Freezable CreateInstanceCore() {
            return new GridLengthAnimation();
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock) {
            double progress = animationClock.CurrentProgress ?? 0;
            if (EasingFunction != null) progress = EasingFunction.Ease(progress);
            double value = From.Value + (To.Value - From.Value) * progress;
            return new GridLength(value, GridUnitType.Pixel);
        }
    }
}
