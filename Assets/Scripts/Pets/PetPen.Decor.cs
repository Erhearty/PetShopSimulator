using UnityEngine;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Pets
{
    /// <summary>
    /// Pen dressing, so a pen reads as somebody's home rather than a sand box with a fence: a bedding
    /// mat tinted per species, a food bowl and a water bowl that show empty or filled with the pen's
    /// feeding state, a species prop (hut, scratching post, chew toy, perch, rock, hay rack), a
    /// corner hay bundle for hay eaters and a low trim inside the fence. There is no vegetation: no plants,
    /// grass or bushes (the grass baked into the pen prefab is removed too, see <see cref="RemoveBakedVegetation"/>).
    ///
    /// Everything is procedural <see cref="MeshBuilder"/> geometry in <see cref="MaterialFactory.Palette"/>
    /// colours, on <see cref="FurnitureFactory.DecorationLayer"/>, with no colliders — so it never blocks
    /// the pets' wander area or the NavMesh — and only MeshRenderers, so FeedbackFX pops still scale it.
    /// It is built once per pen and rebuilt when the pen's size or species changes; it is a child of the
    /// pen, so it moves with it and is destroyed with it.
    /// </summary>
    public partial class PetPen
    {
        /// <summary>Name of the child that holds all the dressing.</summary>
        public const string DecorRootName = "Decor";
        /// <summary>Name of the bedding mat.</summary>
        public const string DecorBedName = "DecorBed";
        /// <summary>Name of the food bowl.</summary>
        public const string DecorFoodBowlName = "DecorFoodBowl";
        /// <summary>Name of the water bowl.</summary>
        public const string DecorWaterBowlName = "DecorWaterBowl";
        /// <summary>Name of the species prop's root.</summary>
        public const string DecorSpeciesPropName = "DecorSpeciesProp";
        /// <summary>Name of the corner hay bundle's root (hay eaters only).</summary>
        public const string DecorCornerName = "DecorCorner";
        /// <summary>Name of each low trim strip along the fence.</summary>
        public const string DecorTrimName = "DecorTrim";
        /// <summary>Suffix of the food or water inside a bowl; shown only while the bowl is filled.</summary>
        public const string DecorBowlFillName = "BowlFill";

        // Layout, as shares of PenSize from the pen centre.
        /// <summary>Bedding mat width, as a share of the pen.</summary>
        private const float BedWidthShare = 0.5f;
        /// <summary>Bedding mat depth, as a share of the pen.</summary>
        private const float BedDepthShare = 0.2f;
        /// <summary>Bedding mat centre, sideways (towards the left fence).</summary>
        private const float BedXShare = -0.2f;
        /// <summary>Bedding mat centre, towards the back fence.</summary>
        private const float BedZShare = -0.36f;
        /// <summary>Food bowl centre, sideways (towards the right fence, clear of the gate).</summary>
        private const float FoodBowlXShare = 0.24f;
        /// <summary>Water bowl centre, sideways (beside the food bowl).</summary>
        private const float WaterBowlXShare = 0.38f;
        /// <summary>Both bowls sit this far towards the gate side.</summary>
        private const float BowlZShare = 0.4f;
        /// <summary>Species prop corner, both axes (back-right).</summary>
        private const float PropCornerShare = 0.34f;
        /// <summary>Corner plant / hay corner, both axes (front-left).</summary>
        private const float CornerShare = 0.4f;
        /// <summary>Trim strips sit just inside the fence.</summary>
        private const float TrimInsetShare = 0.47f;
        /// <summary>The gate gap in the front fence, matching MeshBuilder.CreatePetPen.</summary>
        private const float GateShare = 0.35f;

        // Sizes in metres.
        /// <summary>Height of the pen floor the dressing stands on.</summary>
        private const float FloorTop = 0.04f;
        /// <summary>Bedding mat thickness; sits on the stall beds.</summary>
        private const float BedThickness = 0.03f;
        /// <summary>Lift of the bedding mat above the stall beds.</summary>
        private const float BedLift = 0.035f;
        private const float BowlRadius = 0.09f;
        private const float BowlHeight = 0.05f;
        /// <summary>Radius of the food or water inside a bowl.</summary>
        private const float BowlFillRadius = 0.075f;
        private const float BowlFillHeight = 0.012f;
        /// <summary>How far below the bowl's rim the fill sits.</summary>
        private const float BowlFillDrop = 0.015f;
        private const float TrimHeight = 0.06f;
        private const float TrimThickness = 0.04f;

        private const float HutWidth = 0.34f, HutHeight = 0.22f, HutRoofHeight = 0.05f, HutRoofOverhang = 0.06f;
        private const float PostRadius = 0.05f, PostHeight = 0.5f, PostPadSize = 0.22f, PostPadHeight = 0.03f;
        private const float ToyBallSize = 0.12f, ToyBoneRadius = 0.025f, ToyBoneLength = 0.18f, ToyBoneKnob = 0.06f;
        private const float PerchPostRadius = 0.025f, PerchHeight = 0.32f, PerchSpan = 0.36f, PerchBarRadius = 0.02f;
        private const float RockSize = 0.3f, RockSquash = 0.55f;
        private const float RackWidth = 0.36f, RackHeight = 0.18f, RackDepth = 0.16f, RackHayHeight = 0.06f;
        private const float HayRadius = 0.12f, HayLength = 0.28f;
        private const float QuarterTurn = 90f;

        private Transform   _decorRoot;
        private float       _decorSize = -1f;
        private Pet.Species _decorSpecies;
        private GameObject  _foodFill;
        private GameObject  _waterFill;

        /// <summary>The pen's dressing, or null before it is first built.</summary>
        public Transform DecorRoot => _decorRoot;

        /// <summary>True while the food bowl shows food.</summary>
        public bool FoodBowlFilled => _foodFill != null && _foodFill.activeSelf;

        /// <summary>True while the water bowl shows water.</summary>
        public bool WaterBowlFilled => _waterFill != null && _waterFill.activeSelf;

        /// <summary>Bowls are filled while the pen is fed; they show empty once it needs feeding.</summary>
        private bool BowlsFilled => !NeedsFeeding;

        /// <summary>
        /// Builds the dressing if it is missing or was built for another size or species, then shows the
        /// bowls empty or filled. Called on Start and from <see cref="RefreshUpkeepVisuals"/>.
        /// </summary>
        private void RefreshDecor()
        {
            if (_decorRoot == null || !Mathf.Approximately(_decorSize, PenSize) || _decorSpecies != PenSpecies)
                BuildDecor();
            if (_foodFill  != null) _foodFill.SetActive(BowlsFilled);
            if (_waterFill != null) _waterFill.SetActive(BowlsFilled);
        }

        /// <summary>Throws away any old dressing and builds it afresh for the current size and species.</summary>
        private void BuildDecor()
        {
            if (_decorRoot != null)
            {
                _decorRoot.gameObject.SetActive(false);
                Destroy(_decorRoot.gameObject);
            }
            _decorRoot = new GameObject(DecorRootName).transform;
            _decorRoot.SetParent(transform, false);
            _decorSize    = PenSize;
            _decorSpecies = PenSpecies;

            BuildBed();
            _foodFill  = BuildBowl(DecorFoodBowlName,  FoodBowlXShare,  DecorMaterial(MaterialFactory.Palette.Feed));
            _waterFill = BuildBowl(DecorWaterBowlName, WaterBowlXShare, DecorMaterial(MaterialFactory.Palette.FillLight));
            BuildSpeciesProp();
            BuildCorner();
            BuildTrim();

            MeshBuilder.SetLayerRecursive(_decorRoot.gameObject, FurnitureFactory.DecorationLayer);
        }

        /// <summary>A thin bedding mat across the back of the pen, tinted per species.</summary>
        private void BuildBed()
        {
            var bed = Piece(MeshBuilder.CreateBox(PenSize * BedWidthShare, BedThickness, PenSize * BedDepthShare,
                                                  DecorMaterial(BedColour(PenSpecies)), DecorBedName), _decorRoot);
            bed.localPosition = new Vector3(PenSize * BedXShare, FloorTop + BedLift, PenSize * BedZShare);
        }

        /// <summary>Bedding by species: shavings for small animals, gravel for fish, a blanket for cats and dogs.</summary>
        private static Color BedColour(Pet.Species species) => species switch
        {
            Pet.Species.Rabbit or Pet.Species.Hamster  => MaterialFactory.Palette.OakLight,
            Pet.Species.Cat or Pet.Species.Dog or Pet.Species.Tiger => MaterialFactory.Palette.Sage,
            Pet.Species.Penguin                        => MaterialFactory.Palette.CreamWall,
            Pet.Species.Fish                           => MaterialFactory.Palette.PenSand,
            _                                          => MaterialFactory.Palette.Straw,
        };

        /// <summary>One bowl near the gate; returns its fill, which is shown only while the pen is fed.</summary>
        private GameObject BuildBowl(string name, float xShare, Material fillMaterial)
        {
            var bowl = Piece(MeshBuilder.CreateCylinder(BowlRadius, BowlHeight,
                                                        DecorMaterial(MaterialFactory.Palette.Sunflower), name), _decorRoot);
            bowl.localPosition = new Vector3(PenSize * xShare, FloorTop + BowlHeight * 0.5f, PenSize * BowlZShare);

            // A sibling, not a child: the bowl's non-uniform scale would distort it.
            var fill = Piece(MeshBuilder.CreateCylinder(BowlFillRadius, BowlFillHeight, fillMaterial,
                                                        name + DecorBowlFillName), _decorRoot);
            fill.localPosition = new Vector3(bowl.localPosition.x, FloorTop + BowlHeight - BowlFillDrop,
                                             bowl.localPosition.z);
            return fill.gameObject;
        }

        /// <summary>The species prop in the back-right corner.</summary>
        private void BuildSpeciesProp()
        {
            var prop = new GameObject(DecorSpeciesPropName).transform;
            prop.SetParent(_decorRoot, false);
            prop.localPosition = new Vector3(PenSize * PropCornerShare, FloorTop, -PenSize * PropCornerShare);

            switch (PenSpecies)
            {
                case Pet.Species.Rabbit: case Pet.Species.Hamster:              BuildHut(prop);   break;
                case Pet.Species.Cat: case Pet.Species.Tiger:                   BuildScratchPost(prop); break;
                case Pet.Species.Dog:                                           BuildChewToy(prop); break;
                case Pet.Species.Chicken: case Pet.Species.Parrot:              BuildPerch(prop); break;
                case Pet.Species.Penguin:                                       BuildRock(prop);  break;
                case Pet.Species.Fish:                                          BuildRock(prop);  break;
                default:                                                        BuildHayRack(prop); break;
            }
        }

        /// <summary>A little hideout hut with a sunflower roof.</summary>
        private static void BuildHut(Transform prop)
        {
            Piece(MeshBuilder.CreateBox(HutWidth, HutHeight, HutWidth, DecorMaterial(MaterialFactory.Palette.OakMid), "Hut"), prop);
            var roof = Piece(MeshBuilder.CreateBox(HutWidth + HutRoofOverhang, HutRoofHeight, HutWidth + HutRoofOverhang,
                                                   DecorMaterial(MaterialFactory.Palette.Sunflower), "HutRoof"), prop);
            roof.localPosition = new Vector3(0f, HutHeight + HutRoofHeight * 0.5f, 0f);
        }

        /// <summary>A sisal-wrapped scratching post on a base pad.</summary>
        private static void BuildScratchPost(Transform prop)
        {
            Piece(MeshBuilder.CreateBox(PostPadSize, PostPadHeight, PostPadSize,
                                        DecorMaterial(MaterialFactory.Palette.OakDark), "PostPad"), prop);
            var post = Piece(MeshBuilder.CreateCylinder(PostRadius, PostHeight, DecorMaterial(MaterialFactory.Palette.Straw),
                                                        "ScratchPost"), prop);
            post.localPosition = new Vector3(0f, PostPadHeight + PostHeight * 0.5f, 0f);
        }

        /// <summary>A ball and a bone lying on the floor.</summary>
        private static void BuildChewToy(Transform prop)
        {
            var ball = Piece(MeshBuilder.CreateSphere(ToyBallSize, DecorMaterial(MaterialFactory.Palette.Sunflower), "ToyBall"), prop);
            ball.localPosition = new Vector3(-ToyBoneLength * 0.5f, ToyBallSize * 0.5f, 0f);

            Material bone = DecorMaterial(MaterialFactory.Palette.CreamWall);
            var shaft = Piece(MeshBuilder.CreateCylinder(ToyBoneRadius, ToyBoneLength, bone, "ToyBone"), prop);
            shaft.localPosition    = new Vector3(ToyBoneLength * 0.5f, ToyBoneKnob * 0.5f, 0f);
            shaft.localEulerAngles = new Vector3(0f, 0f, QuarterTurn);
            foreach (float end in new[] { 0f, ToyBoneLength })
            {
                var knob = Piece(MeshBuilder.CreateSphere(ToyBoneKnob, bone, "ToyBoneKnob"), prop);
                knob.localPosition = new Vector3(end, ToyBoneKnob * 0.5f, 0f);
            }
        }

        /// <summary>Two posts and a bar to sit on.</summary>
        private static void BuildPerch(Transform prop)
        {
            Material wood = DecorMaterial(MaterialFactory.Palette.OakDark);
            foreach (float side in new[] { -0.5f, 0.5f })
            {
                var post = Piece(MeshBuilder.CreateCylinder(PerchPostRadius, PerchHeight, wood, "PerchPost"), prop);
                post.localPosition = new Vector3(side * PerchSpan, PerchHeight * 0.5f, 0f);
            }
            var bar = Piece(MeshBuilder.CreateCylinder(PerchBarRadius, PerchSpan, DecorMaterial(MaterialFactory.Palette.OakMid),
                                                       "PerchBar"), prop);
            bar.localPosition    = new Vector3(0f, PerchHeight, 0f);
            bar.localEulerAngles = new Vector3(0f, 0f, QuarterTurn);
        }

        /// <summary>A low, pale rock to stand on.</summary>
        private static void BuildRock(Transform prop)
        {
            var rock = Piece(MeshBuilder.CreateSphere(RockSize, DecorMaterial(MaterialFactory.Palette.CreamWall), "Rock"), prop);
            rock.localScale    = new Vector3(RockSize, RockSize * RockSquash, RockSize);
            rock.localPosition = new Vector3(0f, RockSize * RockSquash * 0.5f, 0f);
        }

        /// <summary>A low wooden rack with hay on top, for grazers.</summary>
        private static void BuildHayRack(Transform prop)
        {
            Piece(MeshBuilder.CreateBox(RackWidth, RackHeight, RackDepth, DecorMaterial(MaterialFactory.Palette.OakMid), "HayRack"), prop);
            var hay = Piece(MeshBuilder.CreateBox(RackWidth, RackHayHeight, RackDepth, DecorMaterial(MaterialFactory.Palette.Straw),
                                                  "HayRackHay"), prop);
            hay.localPosition = new Vector3(0f, RackHeight + RackHayHeight * 0.5f, 0f);
        }

        /// <summary>A hay bundle in the corner for grazers and small animals; nothing for everyone else.</summary>
        private void BuildCorner()
        {
            if (!EatsHay(PenSpecies)) return;

            var corner = new GameObject(DecorCornerName).transform;
            corner.SetParent(_decorRoot, false);
            corner.localPosition = new Vector3(-PenSize * CornerShare, FloorTop, PenSize * CornerShare);

            var hay = Piece(MeshBuilder.CreateCylinder(HayRadius, HayLength, DecorMaterial(MaterialFactory.Palette.Straw),
                                                       "HayBundle"), corner);
            hay.localPosition    = new Vector3(0f, HayRadius, 0f);
            hay.localEulerAngles = new Vector3(0f, 0f, QuarterTurn);
        }

        /// <summary>Name prefix of the grass objects baked into the pen prefab.</summary>
        private const string BakedGrassPrefix = "Grass";

        /// <summary>Removes the grass baked into the pen prefab, so no pen shows vegetation.</summary>
        private void RemoveBakedVegetation()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == transform || !t.name.StartsWith(BakedGrassPrefix)) continue;
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
            }
        }

        /// <summary>Species that get a hay bundle in the corner.</summary>
        private static bool EatsHay(Pet.Species species) => species is Pet.Species.Rabbit or Pet.Species.Hamster
            or Pet.Species.Horse or Pet.Species.Deer or Pet.Species.Chicken;

        /// <summary>A low sage strip just inside the fence on every side, leaving the gate clear.</summary>
        private void BuildTrim()
        {
            float inset = PenSize * TrimInsetShare;
            float span  = inset * 2f;
            Trim(span, new Vector3(0f, 0f, -inset), 0f);
            Trim(span, new Vector3(-inset, 0f, 0f), QuarterTurn);
            Trim(span, new Vector3( inset, 0f, 0f), QuarterTurn);

            float gate = PenSize * GateShare;
            float side = (span - gate) * 0.5f;
            foreach (float sign in new[] { -1f, 1f })
                Trim(side, new Vector3(sign * (gate + side) * 0.5f, 0f, inset), 0f);
        }

        /// <summary>One trim strip of <paramref name="length"/>, turned <paramref name="yaw"/> degrees.</summary>
        private void Trim(float length, Vector3 at, float yaw)
        {
            var strip = Piece(MeshBuilder.CreateBox(length, TrimHeight, TrimThickness,
                                                    DecorMaterial(MaterialFactory.Palette.Sage), DecorTrimName), _decorRoot);
            strip.localPosition    = new Vector3(at.x, FloorTop + TrimHeight * 0.5f, at.z);
            strip.localEulerAngles = new Vector3(0f, yaw, 0f);
        }

        /// <summary>Parents a primitive under <paramref name="parent"/>, keeping its local placement, and removes its collider.</summary>
        private static Transform Piece(GameObject go, Transform parent)
        {
            go.transform.SetParent(parent, false);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);
            return go.transform;
        }

        /// <summary>A matte decor material in a palette colour, cached by colour.</summary>
        private static Material DecorMaterial(Color colour) =>
            MaterialFactory.Get($"pen_decor_{ColorUtility.ToHtmlStringRGB(colour)}", colour, 0f, DecorSmoothness);

        /// <summary>Decor is matte: wood, straw and fabric.</summary>
        private const float DecorSmoothness = 0.15f;
    }
}
