using KamiToolKit.Controls.Builders;
using KamiToolKit.UI.Nodes;
using KamiToolKit.UI.Nodes.ComponentNode;
using Lumina.Text.ReadOnly;
using System.Globalization;

namespace ActionEffectRange.UI
{
    // A slider with a text field next to it to type the exact value
    public class SliderInputNode : ResNode
    {
        private const float InputWidth = 56.0f;
        private const float Gap = 4.0f;

        private readonly float min;
        private readonly float max;
        private readonly int decimals;
        private float value;

        public FloatSliderNode Slider { get; }
        public TextInputNode Input { get; }
        public Action<float>? OnValueUpdate { get; set; }

        public float Value
        {
            get => value;
            set => SetValue(value, notify: false);
        }

        public SliderInputNode(float value, float min, float max, float step, int decimals)
        {
            this.min = min;
            this.max = max;
            this.decimals = decimals;
            this.value = Clamp(value);

            Slider = new FloatSliderNode
            {
                Min = min,
                Max = max,
                Step = step,
                Value = this.value,
                OnValueUpdate = v => SetValue(v, notify: true, updateSlider: false),
            };
            Slider.FloatValueNode.IsVisible = false;
            Slider.AttachNode(this);

            Input = new TextInputNode
            {
                String = Format(this.value),
                MaxCharacters = 8,
                OnInputComplete = ParseInput,
                OnUnfocused = () => ParseInput(Input!.String),
            };
            Input.AttachNode(this);
            Size = new Vector2(100.0f, 24.0f);
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            Slider.Size = new Vector2(MathF.Max(1.0f, Width - InputWidth - Gap), Height);
            Input.Size = new Vector2(InputWidth, Height);
            Input.Position = new Vector2(MathF.Max(0.0f, Width - InputWidth), 0.0f);
        }

        private void ParseInput(ReadOnlySeString input)
        {
            var text = input.ExtractText().Trim().Replace(',', '.');
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                SetValue(parsed, notify: true);
            else
                Input.SetValueSilent(Format(value));
        }

        private void SetValue(float newValue, bool notify, bool updateSlider = true)
        {
            newValue = MathF.Round(Clamp(newValue), decimals);
            var changed = newValue != value;
            value = newValue;
            if (updateSlider) Slider.SetValueSilent(value);
            Input.SetValueSilent(Format(value));
            if (notify && changed) OnValueUpdate?.Invoke(value);
        }

        private float Clamp(float v) => Math.Clamp(v, min, max);

        private string Format(float v)
            => v.ToString($"F{decimals}", CultureInfo.InvariantCulture);
    }

    public static class SliderInputBuilderExtensions
    {
        public static NodeBuilder SliderInput(this NodeBuilder builder, float value,
            float min, float max, float step, int decimals, Action<float> onValueUpdate,
            out SliderInputNode node, Action<SliderInputNode>? configure = null)
        {
            node = new SliderInputNode(value, min, max, step, decimals)
            {
                OnValueUpdate = onValueUpdate,
            };
            configure?.Invoke(node);
            return builder.AddNode(node);
        }

        public static NodeBuilder SliderInput(this NodeBuilder builder, float value,
            float min, float max, float step, int decimals, Action<float> onValueUpdate,
            Action<SliderInputNode>? configure = null)
            => builder.SliderInput(value, min, max, step, decimals, onValueUpdate, out _, configure);
    }
}
