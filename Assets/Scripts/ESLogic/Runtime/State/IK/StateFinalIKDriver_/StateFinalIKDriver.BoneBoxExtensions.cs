using UnityEngine;

namespace ES
{
    internal enum DriverBoneBoxRegion
    {
        Body,
        Head,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg,
    }

    internal sealed class DriverBoneBoxSearchContext
    {
        public Transform bodyPrimary;
        public Transform bodySecondary;
        public Transform bodyTertiary;

        public Transform headPrimary;
        public Transform headSecondary;

        public Transform leftArmPrimary;
        public Transform leftArmSecondary;
        public Transform leftArmTertiary;

        public Transform rightArmPrimary;
        public Transform rightArmSecondary;
        public Transform rightArmTertiary;

        public Transform leftLegPrimary;
        public Transform leftLegSecondary;
        public Transform leftLegTertiary;

        public Transform rightLegPrimary;
        public Transform rightLegSecondary;
        public Transform rightLegTertiary;

        public Transform[] GetHints(DriverBoneBoxRegion region)
        {
            switch (region)
            {
                case DriverBoneBoxRegion.Body:
                    return new[] { bodyPrimary, bodySecondary, bodyTertiary };
                case DriverBoneBoxRegion.Head:
                    return new[] { headPrimary, headSecondary };
                case DriverBoneBoxRegion.LeftArm:
                    return new[] { leftArmPrimary, leftArmSecondary, leftArmTertiary };
                case DriverBoneBoxRegion.RightArm:
                    return new[] { rightArmPrimary, rightArmSecondary, rightArmTertiary };
                case DriverBoneBoxRegion.LeftLeg:
                    return new[] { leftLegPrimary, leftLegSecondary, leftLegTertiary };
                case DriverBoneBoxRegion.RightLeg:
                    return new[] { rightLegPrimary, rightLegSecondary, rightLegTertiary };
                default:
                    return System.Array.Empty<Transform>();
            }
        }
    }

    internal sealed class DriverBoneBoxSearchResult
    {
        public Collider body;
        public Collider head;
        public Collider leftArm;
        public Collider rightArm;
        public Collider leftLeg;
        public Collider rightLeg;

        public int MatchedCount
        {
            get
            {
                int count = 0;
                if (body != null) count++;
                if (head != null) count++;
                if (leftArm != null) count++;
                if (rightArm != null) count++;
                if (leftLeg != null) count++;
                if (rightLeg != null) count++;
                return count;
            }
        }
    }

    internal static class StateFinalIKDriverBoneBoxExtensions
    {
        private static readonly string[] BodyKeywords =
        {
            "body", "torso", "chest", "spine", "pelvis", "hip", "root", "abdomen", "trunk"
        };

        private static readonly string[] HeadKeywords =
        {
            "head", "neck", "face", "skull", "helmet"
        };

        private static readonly string[] ArmKeywords =
        {
            "arm", "upperarm", "lowerarm", "forearm", "elbow", "hand", "shoulder"
        };

        private static readonly string[] LegKeywords =
        {
            "leg", "thigh", "calf", "shin", "knee", "foot", "ankle"
        };

        private static readonly string[] LeftKeywords =
        {
            "left", "_l", "l_", ".l", " l ", "lf", "lh"
        };

        private static readonly string[] RightKeywords =
        {
            "right", "_r", "r_", ".r", " r ", "rf", "rh"
        };

        private static readonly string[] HitBoxKeywords =
        {
            "box", "hit", "hurt", "damage", "collider", "trigger"
        };

        public static DriverBoneBoxSearchResult FindHitReactionBoneBoxesDownward(this Transform current, DriverBoneBoxSearchContext context)
        {
            var result = new DriverBoneBoxSearchResult();
            if (current == null) return result;

            var colliders = current.GetComponentsInChildren<Collider>(true);
            if (colliders == null || colliders.Length == 0) return result;

            var used = new System.Collections.Generic.HashSet<Collider>();

            result.body = PickBestCollider(current, colliders, context.GetHints(DriverBoneBoxRegion.Body), DriverBoneBoxRegion.Body, used);
            if (result.body != null) used.Add(result.body);

            result.head = PickBestCollider(current, colliders, context.GetHints(DriverBoneBoxRegion.Head), DriverBoneBoxRegion.Head, used);
            if (result.head != null) used.Add(result.head);

            result.leftArm = PickBestCollider(current, colliders, context.GetHints(DriverBoneBoxRegion.LeftArm), DriverBoneBoxRegion.LeftArm, used);
            if (result.leftArm != null) used.Add(result.leftArm);

            result.rightArm = PickBestCollider(current, colliders, context.GetHints(DriverBoneBoxRegion.RightArm), DriverBoneBoxRegion.RightArm, used);
            if (result.rightArm != null) used.Add(result.rightArm);

            result.leftLeg = PickBestCollider(current, colliders, context.GetHints(DriverBoneBoxRegion.LeftLeg), DriverBoneBoxRegion.LeftLeg, used);
            if (result.leftLeg != null) used.Add(result.leftLeg);

            result.rightLeg = PickBestCollider(current, colliders, context.GetHints(DriverBoneBoxRegion.RightLeg), DriverBoneBoxRegion.RightLeg, used);
            return result;
        }

        private static Collider PickBestCollider(
            Transform searchRoot,
            Collider[] colliders,
            Transform[] hints,
            DriverBoneBoxRegion region,
            System.Collections.Generic.HashSet<Collider> used)
        {
            Collider best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null) continue;
                if (used != null && used.Contains(collider)) continue;

                int score = ScoreCollider(searchRoot, collider, hints, region);
                if (score <= 0) continue;
                if (score <= bestScore) continue;

                best = collider;
                bestScore = score;
            }

            return best;
        }

        private static int ScoreCollider(Transform searchRoot, Collider collider, Transform[] hints, DriverBoneBoxRegion region)
        {
            int score = 0;
            var target = collider.transform;
            string text = BuildSearchText(collider);

            score += ScoreHintRelations(collider, hints);
            score += ScoreRegionKeywords(text, region);
            score += ScoreHitBoxKeywords(text);
            score += ScoreColliderShape(collider, region);
            score += ScoreSearchDepth(searchRoot, target);

            if (collider.enabled) score += 2;

            return score;
        }

        private static int ScoreHintRelations(Collider collider, Transform[] hints)
        {
            if (hints == null || hints.Length == 0) return 0;

            int best = 0;
            for (int i = 0; i < hints.Length; i++)
            {
                var hint = hints[i];
                if (hint == null) continue;

                int relation = 0;
                if (collider.transform == hint)
                {
                    relation += 220;
                }
                else if (collider.transform.IsChildOf(hint))
                {
                    relation += 200 - (GetHierarchyDistance(hint, collider.transform) * 12);
                }
                else if (hint.IsChildOf(collider.transform))
                {
                    relation += 190 - (GetHierarchyDistance(collider.transform, hint) * 10);
                }

                float sqrDistance = (collider.bounds.center - hint.position).sqrMagnitude;
                if (sqrDistance < 0.01f) relation += 60;
                else if (sqrDistance < 0.09f) relation += 40;
                else if (sqrDistance < 0.25f) relation += 25;
                else if (sqrDistance < 0.64f) relation += 12;

                if (relation > best)
                    best = relation;
            }

            return best;
        }

        private static int ScoreRegionKeywords(string text, DriverBoneBoxRegion region)
        {
            int score = 0;

            switch (region)
            {
                case DriverBoneBoxRegion.Body:
                    score += ScoreTokenMatches(text, BodyKeywords, 16);
                    score -= ScoreTokenMatches(text, HeadKeywords, 8);
                    score -= ScoreTokenMatches(text, ArmKeywords, 6);
                    score -= ScoreTokenMatches(text, LegKeywords, 6);
                    break;
                case DriverBoneBoxRegion.Head:
                    score += ScoreTokenMatches(text, HeadKeywords, 20);
                    score -= ScoreTokenMatches(text, ArmKeywords, 8);
                    score -= ScoreTokenMatches(text, LegKeywords, 8);
                    break;
                case DriverBoneBoxRegion.LeftArm:
                    score += ScoreTokenMatches(text, ArmKeywords, 16);
                    score += ScoreTokenMatches(text, LeftKeywords, 18);
                    score -= ScoreTokenMatches(text, RightKeywords, 22);
                    score -= ScoreTokenMatches(text, LegKeywords, 10);
                    break;
                case DriverBoneBoxRegion.RightArm:
                    score += ScoreTokenMatches(text, ArmKeywords, 16);
                    score += ScoreTokenMatches(text, RightKeywords, 18);
                    score -= ScoreTokenMatches(text, LeftKeywords, 22);
                    score -= ScoreTokenMatches(text, LegKeywords, 10);
                    break;
                case DriverBoneBoxRegion.LeftLeg:
                    score += ScoreTokenMatches(text, LegKeywords, 16);
                    score += ScoreTokenMatches(text, LeftKeywords, 18);
                    score -= ScoreTokenMatches(text, RightKeywords, 22);
                    score -= ScoreTokenMatches(text, ArmKeywords, 10);
                    break;
                case DriverBoneBoxRegion.RightLeg:
                    score += ScoreTokenMatches(text, LegKeywords, 16);
                    score += ScoreTokenMatches(text, RightKeywords, 18);
                    score -= ScoreTokenMatches(text, LeftKeywords, 22);
                    score -= ScoreTokenMatches(text, ArmKeywords, 10);
                    break;
            }

            return score;
        }

        private static int ScoreHitBoxKeywords(string text)
        {
            return ScoreTokenMatches(text, HitBoxKeywords, 6);
        }

        private static int ScoreColliderShape(Collider collider, DriverBoneBoxRegion region)
        {
            int score = 0;

            if (collider is CapsuleCollider) score += 12;
            else if (collider is BoxCollider) score += 10;
            else if (collider is SphereCollider) score += region == DriverBoneBoxRegion.Head ? 12 : 6;

            if (!collider.isTrigger) score += 2;
            return score;
        }

        private static int ScoreSearchDepth(Transform root, Transform target)
        {
            if (root == null || target == null) return 0;
            if (!target.IsChildOf(root) && target != root) return 0;

            int depth = GetHierarchyDistance(root, target);
            return Mathf.Max(0, 18 - depth);
        }

        private static int ScoreTokenMatches(string text, string[] tokens, int weight)
        {
            int score = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (text.Contains(tokens[i]))
                    score += weight;
            }
            return score;
        }

        private static string BuildSearchText(Collider collider)
        {
            string selfName = collider.name;
            string transformName = collider.transform != null ? collider.transform.name : string.Empty;
            string parentName = collider.transform != null && collider.transform.parent != null
                ? collider.transform.parent.name
                : string.Empty;

            return (selfName + "|" + transformName + "|" + parentName).ToLowerInvariant();
        }

        private static int GetHierarchyDistance(Transform ancestor, Transform target)
        {
            if (ancestor == null || target == null) return 99;
            if (ancestor == target) return 0;

            int distance = 0;
            var current = target;
            while (current != null)
            {
                if (current == ancestor) return distance;
                current = current.parent;
                distance++;
            }

            return 99;
        }
    }
}