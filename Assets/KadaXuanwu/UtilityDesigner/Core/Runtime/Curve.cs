using System;

namespace KadaXuanwu.UtilityDesigner.Core {
    public enum CurveShape {
        // y = m * (x - c) + b
        Linear,
        // y = m * (x - c)^k + b, the power taken of the distance and its sign kept
        Polynomial,
        // y = k / (1 + e^(-m * (x - c))) + b: an S, steepness m, middle c, height k
        Logistic,
        // y = 0.5 + ln(x' / (1 - x')) / m + b, x' = x - c + 0.5: the S on its side
        Logit,
        // y = k * sin(m * pi * (x - c)) + b
        Sine,
        // y = k from c on, b below it
        Step,
        // Through keyframes, Hermite between them (Unity's AnimationCurve, unweighted)
        Keys,
    }

    // A keyframe of a Keys curve: where, how high, and the slope coming in and going out.
    public struct CurveKey {
        public float Time;
        public float Value;
        public float InTangent;
        public float OutTangent;

        public CurveKey(float time, float value, float inTangent = 0f, float outTangent = 0f) {
            Time = time;
            Value = value;
            InTangent = inTangent;
            OutTangent = outTangent;
        }
    }

    // A response curve: an input already normalized to 0..1 in, a utility 0..1 out. The shapes take four numbers an
    // optimizer can move (M, K, B, C, Parameters); Keys curves are drawn by hand.
    public sealed class Curve {
        public CurveShape Shape;
        public float M = 1f;
        public float K = 1f;
        public float B;
        public float C;
        public CurveKey[] Keys = Array.Empty<CurveKey>();

        public static Curve Linear(float m = 1f, float b = 0f) => new Curve { Shape = CurveShape.Linear, M = m, B = b };

        public static Curve Polynomial(float k, float m = 1f, float b = 0f, float c = 0f) =>
            new Curve { Shape = CurveShape.Polynomial, M = m, K = k, B = b, C = c };

        public static Curve Logistic(float steepness, float middle = 0.5f) =>
            new Curve { Shape = CurveShape.Logistic, M = steepness, K = 1f, C = middle };

        public static Curve Step(float at) => new Curve { Shape = CurveShape.Step, K = 1f, B = 0f, C = at };

        public static Curve Through(params CurveKey[] keys) => new Curve { Shape = CurveShape.Keys, Keys = keys };

        public float Evaluate(float x) {
            x = Clamp01(x);
            float y;
            switch (Shape) {
                case CurveShape.Linear:
                    y = M * (x - C) + B;
                    break;
                case CurveShape.Polynomial: {
                    float d = x - C;
                    y = M * MathF.Sign(d) * MathF.Pow(MathF.Abs(d), K) + B;
                    break;
                }
                case CurveShape.Logistic:
                    y = K / (1f + MathF.Exp(-M * (x - C))) + B;
                    break;
                case CurveShape.Logit: {
                    float p = Math.Clamp(x - C + 0.5f, 1e-4f, 1f - 1e-4f);
                    y = 0.5f + MathF.Log(p / (1f - p)) / (M == 0f ? 1f : M) + B;
                    break;
                }
                case CurveShape.Sine:
                    y = K * MathF.Sin(M * MathF.PI * (x - C)) + B;
                    break;
                case CurveShape.Step:
                    y = x >= C ? K : B;
                    break;
                default:
                    y = Hermite(x);
                    break;
            }
            return float.IsNaN(y) ? 0f : Clamp01(y);
        }

        private float Hermite(float x) {
            CurveKey[] keys = Keys;
            if (keys.Length == 0) {
                return 0f;
            }
            if (x <= keys[0].Time) {
                return keys[0].Value;
            }
            for (int i = 1; i < keys.Length; i++) {
                if (x > keys[i].Time) {
                    continue;
                }
                CurveKey a = keys[i - 1];
                CurveKey b = keys[i];
                float span = b.Time - a.Time;
                if (span <= 0f) {
                    return b.Value;
                }
                float t = (x - a.Time) / span;
                float t2 = t * t;
                float t3 = t2 * t;
                return (2f * t3 - 3f * t2 + 1f) * a.Value + (t3 - 2f * t2 + t) * span * a.OutTangent
                    + (-2f * t3 + 3f * t2) * b.Value + (t3 - t2) * span * b.InTangent;
            }
            return keys[keys.Length - 1].Value;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
