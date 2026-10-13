using TrashCats.Domain;
using UnityEngine;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Where things stand in the hideout, in the 0..1 space of the background picture bg_hideout (x to the right, y up;
    /// 1024x1536, Docs/ArtDirection/Generated/raw_v3/bg_hideout.png): the back wall with the door, its threshold at
    /// y ≈ 0.35, a pipe on the upper left, ground below. Props are rects; cats are foot points next to their station with
    /// the clip they play, the side they face and, for cats that are on or inside a prop, the prop they draw over.
    /// </summary>
    public static class HideoutLayout
    {
        public const float PictureAspect = 1024f / 1536f;

        /// <summary>Crop the sky first, keep the ground.</summary>
        public static readonly Vector2 Focus = new Vector2(0.5f, 0.2f);

        /// <summary>Picture height per sprite pixel at the front edge: a sitting cat (312 px) is 0.2 of the picture.</summary>
        public const float HeightPerPixel = 0.2f / 312f;

        public static float DepthScale(float y) => Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(0.08f, 0.45f, y));

        public enum CatClip
        {
            Sit,
            Sleep,
            Walk,
            Run,
            Punch,
        }

        /// <summary>How a cat appears at a spot.</summary>
        public readonly struct CatSpot
        {
            public Vector2 Foot { get; }
            public CatClip Clip { get; }
            public bool FaceLeft { get; }
            /// <summary>Drawn over the station prop it stands on / runs in (sleep on the boxes, run in the wheel).</summary>
            public bool OverProp { get; }
            public float Scale { get; }
            public bool Hidden { get; }

            public CatSpot(Vector2 foot, CatClip clip, bool faceLeft = false, bool overProp = false, float scale = 1f, bool hidden = false)
            {
                Foot = foot;
                Clip = clip;
                FaceLeft = faceLeft;
                OverProp = overProp;
                Scale = scale;
                Hidden = hidden;
            }
        }

        /// <summary>Prop rect (min, max) for a station.</summary>
        public static (Vector2 Min, Vector2 Max) Station(ActivityDefinition activity) => activity.Kind switch
        {
            ActivityKind.Scavenge => (new Vector2(0.84f, 0.68f), new Vector2(1.0f, 0.8f)),     // the street gap on the right
            ActivityKind.Rummage => (new Vector2(-0.2f, 0.0f), new Vector2(0.313f, 0.34f)),     // big dumpster, its edge in front
            ActivityKind.Sleep => (new Vector2(0.68f, 0.03f), new Vector2(0.975f, 0.23f)),      // box bed, front right
            _ => activity.TrainedAttribute switch
            {
                AttributeType.Strength => (new Vector2(0.04f, 0.40f), new Vector2(0.266f, 0.82f)), // can on a rope from the pipe
                AttributeType.Agility => (new Vector2(0.40f, 0.2f), new Vector2(0.517f, 0.27f)),   // toy mouse in the yard
                _ => (new Vector2(0.55f, 0.27f), new Vector2(1.044f, 0.67f)),                     // wheel on the right
            },
        };

        /// <summary>Where the cat doing <paramref name="activity"/> stands; <paramref name="index"/> spreads several cats.</summary>
        public static CatSpot Spot(ActivityDefinition activity, int index)
        {
            var shift = new Vector2(-0.09f * index, -0.03f * index);
            return activity.Kind switch
            {
                ActivityKind.Scavenge => new CatSpot(new Vector2(0.93f, 0.68f), CatClip.Walk, hidden: true),
                ActivityKind.Rummage => new CatSpot(new Vector2(0.38f, 0.12f) + shift, CatClip.Walk, faceLeft: true),
                ActivityKind.Sleep => new CatSpot(new Vector2(0.83f, 0.205f) + shift * 0.5f, CatClip.Sleep, overProp: true),
                _ => activity.TrainedAttribute switch
                {
                    // the punching cat stands right of the can and hits to the left
                    AttributeType.Strength => new CatSpot(new Vector2(0.31f, 0.33f) + shift, CatClip.Punch, faceLeft: true),
                    AttributeType.Agility => new CatSpot(new Vector2(0.62f, 0.2f) + shift, CatClip.Walk, faceLeft: true),
                    // inside the wheel's drum (hole at x 0.29..0.65, bottom at 0.32 of the wheel sprite)
                    _ => new CatSpot(new Vector2(0.785f, 0.39f), CatClip.Run, faceLeft: true, overProp: true, scale: 1.05f),
                },
            };
        }

        /// <summary>Idle cats hang around in front of the door.</summary>
        public static CatSpot Idle(int index) => new CatSpot(index switch
        {
            0 => new Vector2(0.47f, 0.36f),
            1 => new Vector2(0.58f, 0.3f),
            2 => new Vector2(0.34f, 0.26f),
            _ => new Vector2(0.5f + 0.08f * (index - 3), 0.24f),
        }, CatClip.Sit);
    }
}
