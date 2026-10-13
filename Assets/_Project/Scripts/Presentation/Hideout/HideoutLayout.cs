using TrashCats.Domain;
using UnityEngine;

namespace TrashCats.Presentation.Hideout
{
    /// <summary>
    /// Where things stand in the hideout, in the 0..1 space of the background picture bg_hideout (x to the right, y up;
    /// 1024x1536, Docs/ArtDirection/Generated/raw_v6/bg_hideout.png): the back wall's base at y ≈ 0.62 with the door at
    /// x 0.46..0.64 and the street gap at the right edge; a wide empty yard below. Props are rects; cats stand on foot
    /// points with the clip they play and the side they face; cats in or on a prop draw over it.
    /// </summary>
    public static class HideoutLayout
    {
        public const float PictureAspect = 1024f / 1536f;

        /// <summary>Crop the sky first, keep the ground.</summary>
        public static readonly Vector2 Focus = new Vector2(0.5f, 0.2f);

        /// <summary>Picture height per sprite pixel at the front edge: a standing cat (~400 px) is about a third of the picture.</summary>
        public const float HeightPerPixel = 0.25f / 312f;

        /// <summary>Things get smaller towards the wall (y 0.62).</summary>
        public static float DepthScale(float y) => Mathf.Lerp(1f, 0.62f, Mathf.InverseLerp(0.05f, 0.62f, y));

        public enum CatClip
        {
            Sit,
            Sleep,
            Walk,
            Run,
            Punch,
            Rope,
            Dig,
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
            /// <summary>Box height (share of the picture height) when it is fixed by a prop, else 0 (from the sprite and depth).</summary>
            public float Height { get; }

            public CatSpot(Vector2 foot, CatClip clip, bool faceLeft = false, bool overProp = false, float scale = 1f, bool hidden = false, float height = 0f)
            {
                Height = height;
                Foot = foot;
                Clip = clip;
                FaceLeft = faceLeft;
                OverProp = overProp;
                Scale = scale;
                Hidden = hidden;
            }
        }

        /// <summary>The wheel, standing on the yard by the left wall.</summary>
        public static readonly (Vector2 Min, Vector2 Max) Wheel = (new Vector2(-0.06f, 0.47f), new Vector2(0.38f, 0.798f));

        /// <summary>Prop rect (min, max) for a station.</summary>
        public static (Vector2 Min, Vector2 Max) Station(ActivityDefinition activity) => activity.Kind switch
        {
            ActivityKind.Scavenge => (new Vector2(0.88f, 0.63f), new Vector2(1.0f, 0.75f)),    // the street gap on the right
            ActivityKind.Rummage => (new Vector2(-0.12f, 0.0f), new Vector2(0.3f, 0.27f)),      // big dumpster, its edge in front
            ActivityKind.Sleep => (new Vector2(0.64f, 0.02f), new Vector2(1.0f, 0.25f)),        // big box bed, front right
            _ => activity.TrainedAttribute switch
            {
                // can on a bracket right of the door: its body at the fist height of a cat standing at the wall
                AttributeType.Strength => (new Vector2(0.7f, 0.655f), new Vector2(0.824f, 0.805f)),
                AttributeType.Agility => (new Vector2(0.4f, 0.64f), new Vector2(0.45f, 0.76f)),     // skipping rope on a nail left of the door
                _ => Wheel,
            },
        };

        /// <summary>Where the cat doing <paramref name="activity"/> stands; <paramref name="index"/> spreads several cats.</summary>
        public static CatSpot Spot(ActivityDefinition activity, int index, string viewKey)
        {
            var shift = new Vector2(-0.09f * index, -0.03f * index);
            return activity.Kind switch
            {
                ActivityKind.Scavenge => new CatSpot(new Vector2(0.94f, 0.63f), CatClip.Walk, hidden: true),
                // head first in the dumpster: the cut line of the clip hides under the bin's front rim; Ponchik's clip
                // is drawn the other way round, so it is mirrored and moved to keep his rump over the opening
                ActivityKind.Rummage => viewKey == "ponchik"
                    ? new CatSpot(new Vector2(0.075f + 0.05f * index, 0.19f), CatClip.Dig, faceLeft: true)
                    : new CatSpot(new Vector2(0.11f + 0.05f * index, 0.19f), CatClip.Dig),
                ActivityKind.Sleep => new CatSpot(new Vector2(0.82f, 0.2f) + shift * 0.5f, CatClip.Sleep, overProp: true),
                _ => activity.TrainedAttribute switch
                {
                    // at arm's length left of the can, hitting to the right
                    AttributeType.Strength => new CatSpot(new Vector2(0.505f, 0.55f) + shift, CatClip.Punch),
                    AttributeType.Agility => new CatSpot(new Vector2(0.5f, 0.06f) + shift, CatClip.Rope),
                    _ => new CatSpot(Vector2.zero, CatClip.Run, faceLeft: true, overProp: true),
                },
            };
        }

        /// <summary>
        /// A cat running in the wheel, relative to the wheel's rect: foot point (0..1 of the rect) and the cat box height
        /// as a share of the rect height. Solved against the sprites (the drum's inner floor of wheel frame 0 and the
        /// union of the cat's run frames): every pixel stays inside the drum, the feet on its floor.
        /// </summary>
        public static (Vector2 Foot, float Height)? RunFit(string viewKey) => viewKey switch
        {
            "ryzhy" => (new Vector2(0.553f, 0.189f), 0.513f),
            "shnyr" => (new Vector2(0.532f, 0.189f), 0.559f),
            "ponchik" => (new Vector2(0.54f, 0.199f), 0.511f),
            _ => null,
        };

        /// <summary>
        /// Idle cats in a row in the middle of the yard: Ponchik in the centre facing the player, Ryzhy on the right
        /// looking right, Shnyr on the left looking left; others fill in behind.
        /// </summary>
        public static CatSpot Idle(string viewKey, int index) => viewKey switch
        {
            "ponchik" => new CatSpot(new Vector2(0.5f, 0.3f), CatClip.Sit),
            "ryzhy" => new CatSpot(new Vector2(0.77f, 0.33f), CatClip.Sit),
            "shnyr" => new CatSpot(new Vector2(0.22f, 0.33f), CatClip.Sit, faceLeft: true),
            _ => new CatSpot(new Vector2(0.4f + 0.1f * index, 0.42f), CatClip.Sit),
        };
    }
}
