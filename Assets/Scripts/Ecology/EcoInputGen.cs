using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EcoInputGen : MonoBehaviour
{
    private enum BoneGroup
    {
        Unknown,
        Torso,          //Chest and belly combined
        Head,
        Neck,
        Tail,
        LimbGirdle,     //Bigger bones like shoulders and pelvis
        LimbExtremity   //Ankles and toes
    }

    [Header("Generator References")]
    public SkelGen skeletonGen;
    public MeshGen meshGen;
    private Dictionary<string, float> defaultRadiiCache = new Dictionary<string, float>();

    [Header("Environmental Pressures")]
    [Range(-20f, 40f)] public float temperature = 20f;
    [Range(0.5f, 3f)] public float gravity = 1f;
    [Range(0f, 1f)] public float terrainRoughness = 0.5f;
    [Range(0f, 10f)] public float foodCanopyHeight = 1f;
    [Range(0f, 1f)] public float humidity = 0.5f;
    [Range(0f, 1f)] public float forestDensity = 0.5f;
    [Range(0f, 1f)] public float terrainHardness = 0.5f;
    [Range(0f, 1f)] public float foodDistance = 0.5f;

    [Header("Non-Environmental Controls")]
    public bool useRandomSeed = false;
    public int randomSeed = 12345;
    [Range(0.1f, 3f)] public float minGlobalScale = 0.1f;
    [Range(0.1f, 3f)] public float maxGlobalScale = 1.5f;
    [Range(3, 20)] public int minBodyLength = 3;
    [Range(3, 20)] public int maxBodyLength = 10;

    [ContextMenu("Evolve")]
    public void Evolve()
    {
        if (skeletonGen == null || meshGen == null)
        {
            Debug.LogError("Assign references to skeleton and mesh generators");
            return;
        }


        //By using a seed I can apply the gausse hypothesis while still offering a user the option to keep things consistent
        if (useRandomSeed)
        {
            randomSeed = System.Environment.TickCount;
        }
        Random.InitState(randomSeed);

        //Prep
        CacheDefaultRadius();
        Reset();
        
        //Run ecology
        EcoRules();
        TerrainAdaption();
        GirraffeBalancer();
        DistanceToFood();

        //Generate it
        SanityClamps(); 
        AlterMeshProfiles();
        skeletonGen.GenSkeleton();
    }


    //This function allows mesh adjustments relating to defaults
    private void CacheDefaultRadius()
    {
        if (defaultRadiiCache.Count == 0 && meshGen.boneProfiles.Count > 0)
        {
            foreach (var profile in meshGen.boneProfiles)
            {
                defaultRadiiCache[profile.nameKeyword] = profile.radiusMultiplier;
            }
        }
    }


    //After having trouble with the skeleton regenreting resetting like this helped
    private void Reset()
    {
        skeletonGen.frontLimbCompression = 0f;
        skeletonGen.rearLimbCompression = 0f;
        skeletonGen.legSplayAngle = 0f;
        skeletonGen.neckInclineAngle = 0f;
        skeletonGen.tailCurlPitch = 0f;
        skeletonGen.ChestRatio = 0.4f; 
    }


    //Applies bergman and allens rules
    private void EcoRules()
    {
        float tNorm = Mathf.InverseLerp(-20f, 40f, temperature);
        float coldStress = (1f - tNorm) * (1f - tNorm); 
        
        skeletonGen.globalScale = Mathf.Lerp(maxGlobalScale, minGlobalScale, tNorm) * EcoVariance(1f, 0.08f); 
        skeletonGen.bodyLength = Mathf.RoundToInt(Mathf.Lerp(minBodyLength, maxBodyLength, tNorm) * EcoVariance(1f, 0.15f));
        
        skeletonGen.ChestRatio = Mathf.Lerp(0.38f, 0.5f, coldStress);

        float appendageScale = Mathf.Lerp(0.7f, 1.3f, tNorm);
        if (humidity > 0.7f && tNorm > 0.5f) appendageScale *= 0.85f; 

        skeletonGen.frontLimbLengthScale = appendageScale * EcoVariance(1f, 0.1f);
        skeletonGen.rearLimbLengthScale = appendageScale * EcoVariance(1f, 0.1f);

        skeletonGen.tailLength = Mathf.RoundToInt(Mathf.Lerp(4, 12, tNorm));
        
        float totalDesiredCurve = Mathf.Lerp(5f, 25f, tNorm); 
        skeletonGen.tailCurlPitch = (totalDesiredCurve / Mathf.Max(1f, skeletonGen.tailLength)) * EcoVariance(1f, 0.1f);
    }

    //Changes the legs to adapt creatures to rough terrain
    private void TerrainAdaption()
    {
        float physicalStressRaw = Mathf.Clamp01((gravity * 0.6f) + (terrainRoughness * 0.4f));
        float physicalStressCurve = physicalStressRaw * physicalStressRaw; 

        skeletonGen.shoulderWidth = Mathf.Lerp(0.18f, 0.32f, physicalStressCurve) * EcoVariance(1f, 0.08f);
        skeletonGen.pelvicWidth = Mathf.Lerp(0.15f, 0.28f, physicalStressCurve) * EcoVariance(1f, 0.08f);
        
        float gravityNorm = Mathf.InverseLerp(1f, 3f, gravity);
        float gravityStress = gravityNorm * gravityNorm;
        skeletonGen.legSplayAngle = Mathf.Lerp(0f, 15f, gravityStress); 

        if (terrainHardness < 0.3f)
        {
            skeletonGen.legType = SkelGen.LegGait.Plantigrade;
            skeletonGen.frontLimbRatios = new Vector4(1f, 1f, 0.85f, 0.85f);
            skeletonGen.rearLimbRatios = new Vector4(1f, 1f, 0.85f, 0.85f);
        }
        else if (terrainHardness > 0.7f && terrainRoughness < 0.4f)
        {
            skeletonGen.legType = SkelGen.LegGait.Unguligrade;
            skeletonGen.frontLimbRatios = new Vector4(0.95f, 1.05f, 1.3f, 0.2f);
            skeletonGen.rearLimbRatios = new Vector4(0.95f, 1.05f, 1.3f, 0.2f);
        }
        else 
        {
            skeletonGen.legType = SkelGen.LegGait.Digitigrade;
            skeletonGen.frontLimbRatios = new Vector4(1f, 1f, 1.15f, 0.4f);
            skeletonGen.rearLimbRatios = new Vector4(1f, 1.05f, 1.15f, 0.4f);
        }

        if (terrainRoughness > 0.6f)
        {
            skeletonGen.frontLimbCompression = Random.Range(3f, 12f);
            skeletonGen.rearLimbCompression = Random.Range(3f, 12f);
        }
    }

    private float ShoulderHeight()
    {
        float legDrop = skeletonGen.CalculateLegDrop(true);
        return legDrop * skeletonGen.globalScale;
    }

    //This is to help prevent super long necks forming that look unbalanced
    private void GirraffeBalancer()
    {
        float shoulderH = Mathf.Max(0.1f, ShoulderHeight());
        float reachRatio = Mathf.Clamp(foodCanopyHeight / shoulderH, 0.4f, 2.2f);
        float heightNorm = Mathf.InverseLerp(0.4f, 2.2f, reachRatio);
        
        
        float tNorm = Mathf.InverseLerp(-20f, 40f, temperature); 
        float coldPenalty = Mathf.Lerp(0.5f, 1.0f, tNorm); 

        skeletonGen.neckBasePitch = 25f;
        skeletonGen.neckShapeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        if (reachRatio > 1.1f)
        {
            
            int maxNeck = Mathf.RoundToInt(Mathf.Lerp(1, 8, heightNorm) * coldPenalty);
            
            if (Random.value > 0.4f)
            {
                skeletonGen.neckLength = Mathf.Clamp(maxNeck, 1, 8);
                skeletonGen.neckInclineAngle = Mathf.Lerp(35f, 65f, heightNorm) * EcoVariance(1f, 0.05f);
                skeletonGen.neckBasePitch = Mathf.Lerp(25f, 45f, heightNorm);
            }
            else
            {
                skeletonGen.neckLength = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2, 5, heightNorm) * coldPenalty), 1, 5);
                skeletonGen.neckInclineAngle = Mathf.Lerp(40f, 60f, heightNorm);
                skeletonGen.rearLimbRatios.x *= 1.15f; 
            }
        }
        else
        {
            
            skeletonGen.neckLength = Mathf.RoundToInt(Mathf.Lerp(2, 4, reachRatio) * coldPenalty); 
            skeletonGen.neckInclineAngle = Mathf.Lerp(-5f, 10f, reachRatio);
            skeletonGen.neckBasePitch = Mathf.Lerp(15f, 25f, reachRatio);
        }

       
        float frontHeavyRatio = (float)skeletonGen.neckLength / Mathf.Max(1, skeletonGen.bodyLength);
        if (frontHeavyRatio > 1.2f)
        {
            if (Random.value > 0.5f)
                skeletonGen.tailLength = Mathf.Clamp(skeletonGen.tailLength + Random.Range(2, 6), 4, 18);
            else
                skeletonGen.tailLength = Mathf.Clamp(skeletonGen.tailLength - Random.Range(0, 2), 2, 8);
        }
    }


    //Effects how enduring the creature has to be which effects limb and tail scales as well as chest size
    private void DistanceToFood()
    {
        float travelStressCurve = foodDistance * foodDistance; 

        if (foodDistance > 0.6f)
        {
            if (Random.value > 0.5f)
            {
                skeletonGen.frontLimbLengthScale *= EcoVariance(Mathf.Lerp(1.0f, 1.12f, travelStressCurve), 0.05f);
                skeletonGen.rearLimbLengthScale *= EcoVariance(Mathf.Lerp(1.0f, 1.12f, travelStressCurve), 0.05f);
            }

            skeletonGen.spineShapeCurve = AnimationCurve.Linear(0, 0, 1, 0); 
            skeletonGen.ChestRatio = Mathf.Clamp(skeletonGen.ChestRatio * Mathf.Lerp(1.0f, 1.2f, travelStressCurve), 0.35f, 0.55f);
        }

        if (forestDensity > 0.6f)
        {
            if (Random.value > 0.3f)
            {
                skeletonGen.bodyLength = Mathf.Clamp(Mathf.RoundToInt(skeletonGen.bodyLength * 0.8f), 3, 7);
                skeletonGen.tailLength = Mathf.Clamp(Mathf.RoundToInt(skeletonGen.tailLength * 0.75f), 1, 10);
            }
            skeletonGen.legSplayAngle *= 0.7f; 
        }
    }

    //Just prevents wacky results
    private void SanityClamps()
    {
        skeletonGen.globalScale = Mathf.Clamp(skeletonGen.globalScale, 0.7f, 1.7f);
        skeletonGen.bodyLength = Mathf.Clamp(skeletonGen.bodyLength, 3, 8);
        skeletonGen.neckLength = Mathf.Clamp(skeletonGen.neckLength, 1, 8);
        skeletonGen.tailLength = Mathf.Clamp(skeletonGen.tailLength, 1, 16);
        skeletonGen.frontLimbLengthScale = Mathf.Clamp(skeletonGen.frontLimbLengthScale, 0.6f, 1.5f);
        skeletonGen.rearLimbLengthScale = Mathf.Clamp(skeletonGen.rearLimbLengthScale, 0.6f, 1.5f);
        skeletonGen.ChestRatio = Mathf.Clamp(skeletonGen.ChestRatio, 0.35f, 0.55f);
    }


    //Applies the results of the rules to the mesh profile
    private void AlterMeshProfiles()
    {
        if (meshGen.boneProfiles.Count == 0) return;

        float tNorm = Mathf.InverseLerp(-20f, 40f, temperature);
        
        float heatExaggeration = tNorm * tNorm; 
        float travelExaggeration = foodDistance * foodDistance;

        
        float thicknessRatio = Mathf.Lerp(0.20f, 0.10f, tNorm); 
        thicknessRatio *= Mathf.Lerp(1.1f, 0.9f, travelExaggeration); 
        
        meshGen.baseRadius = Mathf.Clamp(skeletonGen.globalScale * thicknessRatio, 0.04f, 0.35f);

        float targetSpineBaseline = 1.0f;
        foreach (var pair in defaultRadiiCache)
        {
            if (pair.Key.ToLower().Contains("spine"))
            {
                targetSpineBaseline = pair.Value;
                break;
            }
        }

        foreach (var profile in meshGen.boneProfiles)
        {
            string kw = profile.nameKeyword.ToLower();
            float baseline = defaultRadiiCache.ContainsKey(profile.nameKeyword) ? defaultRadiiCache[profile.nameKeyword] : 1.0f;

            bool isChest = kw.Contains("chest");
            
         
            float effectiveBaseline = isChest ? Mathf.Lerp(targetSpineBaseline, baseline, tNorm) : baseline;

            profile.radiusMultiplier = effectiveBaseline;
            profile.crossSectionScale = Vector2.one;
            profile.positionOffset = Vector3.zero;
            profile.thicknessCurve = AnimationCurve.Linear(0, 1, 1, 1);

            float tempMod = Mathf.Lerp(1.12f, 0.88f, tNorm); 
            BoneGroup boneGroup = GetBoneGroup(profile.nameKeyword);

            switch (boneGroup)
            {
                case BoneGroup.Torso:
                   
                    float torsoBulk = Mathf.Lerp(1.6f, 0.9f, tNorm); 
                    profile.radiusMultiplier *= (tempMod * torsoBulk);
                    
                   
                    if (isChest)
                    {
                        
                        profile.radiusMultiplier *= Mathf.Lerp(1.0f, 1.4f, heatExaggeration);
                    }
                    else if (kw.Contains("spine")) 
                    {
                        
                        profile.radiusMultiplier *= Mathf.Lerp(1.0f, 0.8f, heatExaggeration);
                    }

                    float currentRadius = meshGen.baseRadius * profile.radiusMultiplier;
                    
                    
                    float targetJointWidth = isChest ? skeletonGen.shoulderWidth : skeletonGen.pelvicWidth;
                    
                   
                    float targetWidthRadius = targetJointWidth / 2f; 
                    float minWidthScale = targetWidthRadius / Mathf.Max(0.01f, currentRadius);

                    
                    float bodyWidthScale = 1.0f;
                    float bodyHeightScale = 0.95f * Mathf.Lerp(1.0f, 1.2f, travelExaggeration);

                   
                    if (isChest)
                    {
                        bodyHeightScale *= Mathf.Lerp(1.0f, 1.35f, heatExaggeration);
                        bodyWidthScale *= Mathf.Lerp(1.0f, 1.15f, heatExaggeration);
                    }
                    else if (kw.Contains("spine"))
                    {
                        bodyHeightScale *= Mathf.Lerp(1.0f, 0.85f, heatExaggeration);
                        bodyWidthScale *= Mathf.Lerp(1.0f, 0.9f, heatExaggeration);
                    }

                  
                    bodyWidthScale = Mathf.Clamp(bodyWidthScale, 0.6f, 2.2f);
                    bodyHeightScale = Mathf.Clamp(bodyHeightScale, 0.6f, 2.2f);

                    
                    bodyWidthScale = Mathf.Max(bodyWidthScale, minWidthScale);

                    profile.crossSectionScale = new Vector2(bodyWidthScale, bodyHeightScale);

                   
                    float expectedSpineRadiusMult = targetSpineBaseline * tempMod * torsoBulk * Mathf.Lerp(1.0f, 0.8f, heatExaggeration);
                    float referenceSpineTop = meshGen.baseRadius * expectedSpineRadiusMult;
                    float scaledYHeight = currentRadius * profile.crossSectionScale.y;
                    profile.positionOffset = new Vector3(0, referenceSpineTop - scaledYHeight, 0);

                    if (humidity < 0.3f && tNorm > 0.6f) 
                    {
                        profile.chainTaperCurve = new AnimationCurve(
                            new Keyframe(0f, 1f),
                            new Keyframe(0.5f, 1.2f), 
                            new Keyframe(1f, 1f)
                        );
                    }
                    else
                    {
                        profile.chainTaperCurve = AnimationCurve.Linear(0, 1, 1, 1);
                    }
                    break;

                case BoneGroup.Head:
                    float sensoryPressure = Mathf.Lerp(1.0f, 1.25f, travelExaggeration);
                    float biteConstraint = Mathf.Lerp(1.15f, 0.8f, terrainHardness); 
                    
                    
                    float neckToBodyRatio = (float)skeletonGen.neckLength / Mathf.Max(1, skeletonGen.bodyLength);
                    float longNeckShrinkage = Mathf.Lerp(1.0f, 0.45f, Mathf.InverseLerp(0.5f, 1.8f, neckToBodyRatio));

                    profile.radiusMultiplier *= (sensoryPressure * biteConstraint * longNeckShrinkage);
                    
                    if (terrainHardness > 0.6f) {
                        profile.crossSectionScale = new Vector2(1.15f, 1.05f);
                    } else {
                        profile.crossSectionScale = new Vector2(0.85f, 1.2f); 
                    }
                    break;

                case BoneGroup.Neck:
                    profile.radiusMultiplier *= tempMod;
                    float frontHeavyNeckRatio = (float)skeletonGen.neckLength / Mathf.Max(1, skeletonGen.bodyLength);
                    
                    if (frontHeavyNeckRatio > 1.2f)
                    {
                        profile.chainTaperCurve = new AnimationCurve(new Keyframe(0, 1.2f), new Keyframe(1, 0.6f));
                    }
                    else
                    {
                        profile.chainTaperCurve = new AnimationCurve(new Keyframe(0, 1.0f), new Keyframe(1, 0.6f));
                    }
                    break;

                case BoneGroup.Tail:
                    float frontHeavyTailRatio = (float)skeletonGen.neckLength / Mathf.Max(1, skeletonGen.bodyLength);
                    
                    if (frontHeavyTailRatio > 1.2f && skeletonGen.tailLength <= 10)
                    {
                        profile.radiusMultiplier *= 1.05f; 
                        profile.chainTaperCurve = new AnimationCurve(new Keyframe(0, 1.15f), new Keyframe(1, 0.25f));
                    }
                    else
                    {
                        profile.radiusMultiplier *= tempMod;
                        profile.chainTaperCurve = new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0.15f));
                    }
                    break;

                case BoneGroup.LimbGirdle:
                    float girdleGravityMod = Mathf.Lerp(1.0f, 1.1f, gravity / 3f);
                    profile.radiusMultiplier *= (tempMod * girdleGravityMod);
                    break;

                case BoneGroup.LimbExtremity:
                    if (terrainHardness < 0.3f) 
                        profile.crossSectionScale = new Vector2(1.35f, 0.7f); 
                    break;
            }
        }
    }

    private BoneGroup GetBoneGroup(string nameKeyword)
    {
        if (string.IsNullOrEmpty(nameKeyword)) return BoneGroup.Unknown;
        
        string kw = nameKeyword.ToLower();

        if (kw.Contains("spine") || kw.Contains("chest")) return BoneGroup.Torso;
        if (kw.Contains("head")) return BoneGroup.Head;
        if (kw.Contains("neck")) return BoneGroup.Neck;
        if (kw.Contains("tail")) return BoneGroup.Tail;
        if (kw.Contains("pelvis") || kw.Contains("shoulder")) return BoneGroup.LimbGirdle;
        if (kw.Contains("toebase") || kw.Contains("ankle")) return BoneGroup.LimbExtremity;

        return BoneGroup.Unknown;
    }

    //A little randomness helps with the guase hypothesis 
    private float EcoVariance(float val, float variance = 0.1f)
    {
        return val * Random.Range(1f - variance, 1f + variance);
    }

}