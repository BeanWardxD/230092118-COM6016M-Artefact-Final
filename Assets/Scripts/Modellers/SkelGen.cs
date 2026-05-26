using UnityEngine;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Animations.Rigging;

public class SkelGen : MonoBehaviour
{
    [System.Serializable]
    public class LRule
    {
        public char input;
        public string output;
    }

    [Header("L-System Rules")]
    public string axiom = "HNFBRT";
    public List<LRule> customRules = new List<LRule>();
    public BoneRenderer bRenderer;
    public GameObject bonePrefab;
    private List<Transform> allBones = new List<Transform>();

    [Header("Dynamic Controls")]
    public float globalScale = 1f;
    [Range(1, 20)] public int neckLength = 3;
    [Range(1, 20)] public int bodyLength = 4;
    [Range(0, 30)] public int tailLength = 6;
    [Range(0.1f, 0.5f)] public float ChestRatio = 0.25f;

    [Header("Spine & Neck Shape")]
    public AnimationCurve spineShapeCurve = AnimationCurve.EaseInOut(0, 0, 1, 0);
    public AnimationCurve neckShapeCurve  = AnimationCurve.EaseInOut(0, 0, 1, 1);
  
    [Range(0f, 60f)] public float neckBasePitch = 30f;
    [Range(0f, 60f)] public float spinePitchAngle = 0f;
   
    [Range(-45f, 45f)] public float neckInclineAngle = 20f;
    private float vertebraeLength = 0.25f;

    [Header("Tail Settings")]
    [Range(-30f, 30f)] public float tailCurlPitch = 5f;
    public AnimationCurve tailTaperCurve = AnimationCurve.Linear(0, 1, 1, 0.3f);

    [Header("Transverse Widths")]
    [Range(0.05f, 1f)] public float shoulderWidth = 0.1f;
    [Range(0.05f, 1f)] public float pelvicWidth   = 0.05f;

    public enum LegGait { Plantigrade, Digitigrade, Unguligrade }
    public LegGait legType = LegGait.Digitigrade;
    [Range(0f, 90f)] public float legSplayAngle = 0f;

    [Header("Limb Proportional Ratios")]
    public Vector4 frontLimbRatios = new Vector4(1f, 1f, 1f, 1f);
    public Vector4 rearLimbRatios  = new Vector4(1f, 1f, 1f, 1f);

    [Header("Limb Dynamics")]
    [Range(0.2f, 3f)] public float frontLimbLengthScale = 1f;
    [Range(0.2f, 3f)] public float rearLimbLengthScale  = 1f;
    [Range(-45f, 45f)] public float frontLimbCompression = 0f;
    [Range(-45f, 45f)] public float rearLimbCompression  = 0f;

    private int iterations = 1;

    [ContextMenu("Generate")]
    //Actual call for the skeleton to be made and sets out the rules
    public void GenSkeleton()
    {
        
        var children = new List<Transform>();
        foreach (Transform child in transform) children.Add(child);
        foreach (var child in children)
        {
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        allBones.Clear();

       
        float frontDrop  = CalculateLegDrop(true);
        float rearDrop   = CalculateLegDrop(false);
        float spineLength = Mathf.Max(0.001f, bodyLength * vertebraeLength * globalScale);

        float pitchSin = (frontDrop - rearDrop) / spineLength;
        pitchSin = Mathf.Clamp(pitchSin, -1f, 1f);
        spinePitchAngle = -(Mathf.Asin(pitchSin) * Mathf.Rad2Deg);
       

        //Active rules decide how the axiom is to be expanded out 
        Dictionary<char, string> activeRules = new Dictionary<char, string>();
        foreach (var r in customRules) { activeRules[r.input] = r.output; }

        activeRules['N'] = RepeatString("N", neckLength);

        activeRules['B'] = RepeatString("S", Mathf.Max(0, bodyLength - 1));
        
        activeRules['T'] = RepeatString("t", tailLength);

        activeRules['F'] = "X[f][g]";
        
        activeRules['R'] = "Y[b][k]";

        string path = GenPath(axiom, activeRules);
        Interpret(path);

    
        float minHeight = float.MaxValue;
        foreach (Transform bone in allBones)
        {
            if (bone.position.y < minHeight) minHeight = bone.position.y;
        }

        float groundOffset = this.transform.position.y - minHeight;
        Transform head = allBones.Find(b => b.name == "Head");

        if (head != null)
        {
            head.position += new Vector3(0, groundOffset, 0);
        }
      
        //Once the job is done the mesh has a guideline so its called after the skeleton rather than before
        MeshGen meshGen = Object.FindFirstObjectByType<MeshGen>();
        if (meshGen != null && head != null)
        {
            meshGen.GenMesh(head);
        }
    }

    string RepeatString(string input, int count)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < count; i++) sb.Append(input);
        return sb.ToString();
    }

    string GenPath(string axiom, Dictionary<char, string> rules)
    {
        string current = axiom;
        for (int i = 0; i < iterations; i++)
        {
            StringBuilder next = new StringBuilder();
            foreach (char c in current)
            {
                next.Append(rules.ContainsKey(c) ? rules[c] : c.ToString());
            }
            current = next.ToString();
        }
        return current;
    }

    void Interpret(string path)
    {
        Stack<Transform> savePoints = new Stack<Transform>();
        Transform currentParent = this.transform;

        int currentSpineIndex = 0;
        int currentNeckIndex  = 0;
        int currentTailIndex  = 0;
        //Each of these cases is for a different part of the skeleton so when the axiom reaches its part it knows how to do the bone
        foreach (char c in path)
        {
            switch (c)
            {
                //Controls how the head is made
                case 'H':
                    currentParent = CreateBone(currentParent, "Head", Vector3.zero, Quaternion.identity);
                    break;

                //This one is for the neck
                case 'N':
                {
                    float currentProgress = neckLength > 1 ? (float)currentNeckIndex / (neckLength - 1) : 0f;
                    float prevProgress    = currentNeckIndex > 0 ? (float)(currentNeckIndex - 1) / (neckLength - 1) : 0f;

                    float shapedTCurr    = neckShapeCurve.Evaluate(currentProgress);
                    float steepnessCurr  = 4f * shapedTCurr * (1f - shapedTCurr);
                    float currentTargetPitch = -(Mathf.Lerp(neckInclineAngle, 0f, shapedTCurr) + (steepnessCurr * neckBasePitch));

                    float prevTargetPitch = 0f;
                    if (currentNeckIndex > 0)
                    {
                        float shapedTPrev   = neckShapeCurve.Evaluate(prevProgress);
                        float steepnessPrev = 4f * shapedTPrev * (1f - shapedTPrev);
                        prevTargetPitch = -(Mathf.Lerp(neckInclineAngle, 0f, shapedTPrev) + (steepnessPrev * neckBasePitch));
                    }

                    float segmentPitch = currentTargetPitch - prevTargetPitch;

                    Quaternion localRotation = Quaternion.Euler(segmentPitch, 0, 0);
                    Vector3 neckOffset = localRotation * (-Vector3.forward * vertebraeLength * globalScale);
                    currentParent = CreateBone(currentParent, "Neck_Seg", neckOffset, localRotation);
                    currentNeckIndex++;
                }
                break;

                case 'X':
                {
                    
                    float currentDip    = spineShapeCurve.Evaluate(0f);
                    float localPitch    = spinePitchAngle + currentDip;

                    Quaternion absoluteRotation  = this.transform.rotation * Quaternion.Euler(localPitch, 0, 0);
                    Quaternion correctionRotation = Quaternion.Inverse(currentParent.rotation) * absoluteRotation;

                    Vector3 spineOffset = correctionRotation * (-Vector3.forward * vertebraeLength * globalScale);
                    currentParent = CreateBone(currentParent, "Chest_Seg", spineOffset, correctionRotation);
                    
                  
                    currentSpineIndex++;
                }
                break;

                //Generates spine parts and names them differently so the mesh genrator can do different chest and belly sizes
                case 'S':
                {
                    
                    float spineProgress = bodyLength > 1 ? (float)currentSpineIndex / (bodyLength - 1) : 0f;
                    float currentDip    = spineShapeCurve.Evaluate(spineProgress);

                    Quaternion localRotation = Quaternion.Euler(currentDip, 0, 0);
                    Vector3 spineOffset = localRotation * (-Vector3.forward * vertebraeLength * globalScale);

                    if (currentSpineIndex < bodyLength * ChestRatio)
                    {
                        currentParent = CreateBone(currentParent, "Chest_Seg", spineOffset, localRotation);
                    }
                    else
                    {
                        currentParent = CreateBone(currentParent, "Spine_Seg", spineOffset, localRotation);
                    }
                    currentSpineIndex++;
                }
                break;
                //Makes sure the tail starts after the rear legs
                case 'Y':
                {
                    Quaternion localRotation = Quaternion.Euler(tailCurlPitch, 0, 0);
                    Vector3 tailOffset = localRotation * (-Vector3.forward * vertebraeLength * globalScale);
                    currentParent = CreateBone(currentParent, "Tail_Seg", tailOffset, localRotation);
                }
                break;

                //Controls the tail length taper and offset
                case 't':
                {
                    float tailProgress = tailLength > 1 ? (float)currentTailIndex / (tailLength - 1) : 0f;
                    float taperScale   = tailTaperCurve.Evaluate(tailProgress);

                    Quaternion localRotation = Quaternion.Euler(tailCurlPitch, 0, 0);
                    Vector3 tailOffset = localRotation * (-Vector3.forward * vertebraeLength * taperScale * globalScale);

                    currentParent = CreateBone(currentParent, "Tail_Seg", tailOffset, localRotation);
                    currentTailIndex++;
                }
                break;

                case 'f': BuildLeg(currentParent, -1, true);  break;
                case 'g': BuildLeg(currentParent,  1, true);  break;
                case 'b': BuildLeg(currentParent, -1, false); break;
                case 'k': BuildLeg(currentParent,  1, false); break;

                case '[': savePoints.Push(currentParent); break;
                case ']': if (savePoints.Count > 0) currentParent = savePoints.Pop(); break;
            }
        }

        if (bRenderer != null) bRenderer.transforms = allBones.ToArray();
    }


    //This is used to determine how far the legs will be down the y axis so the spine can be adjusted
    public float CalculateLegDrop(bool isFront)
    {
        Vector4 proportions = isFront ? frontLimbRatios : rearLimbRatios;
        float lScale = isFront ? frontLimbLengthScale : rearLimbLengthScale;
        float comp   = isFront ? frontLimbCompression  : rearLimbCompression;

        
        float horizontalWidth = isFront ? shoulderWidth : pelvicWidth;

        Quaternion splayRot = Quaternion.Euler(0, 0, -legSplayAngle);

        float cosVal    = Mathf.Max(0.1f, Mathf.Abs(Mathf.Cos(comp * Mathf.Deg2Rad)));
        float compFactor = 1.0f / cosVal;

        Vector3[] simPos = new Vector3[6];
        simPos[0] = Vector3.zero;
        simPos[1] = new Vector3(horizontalWidth, 0, 0);

        for (int j = 0; j < 4; j++)
        {
            Vector3 baseOffset  = GetLegType(j, legType);
            float   scalar      = (j == 0) ? proportions.x : (j == 1 ? proportions.y : (j == 2 ? proportions.z : proportions.w));
            float   lengthComp  = (j == 0 || j == 1) ? compFactor : 1.0f;
            Vector3 v           = baseOffset * scalar * lScale * globalScale * lengthComp;

            Vector3 segmentVec = Vector3.zero;
            if      (j == 0) segmentVec = splayRot * (Quaternion.Euler(comp,  0, 0) * v);
            else if (j == 1) segmentVec = splayRot * (Quaternion.Euler(-comp, 0, 0) * v);
            else if (j == 2) segmentVec = (legType == LegGait.Plantigrade) ? new Vector3(0, 0, v.magnitude) : splayRot * v;
            else if (j == 3) segmentVec = (legType == LegGait.Plantigrade || legType == LegGait.Digitigrade) ? new Vector3(0, 0, v.magnitude) : splayRot * v;

            simPos[j + 2] = simPos[j + 1] + segmentVec;
        }

        return Mathf.Abs(simPos[5].y);
    }


    //Builds out the leg
    void BuildLeg(Transform root, float side, bool isFront)
    {
        float horizontalWidth = isFront ? shoulderWidth : pelvicWidth;
        Vector4 proportions   = isFront ? frontLimbRatios : rearLimbRatios;
        float lScale          = isFront ? frontLimbLengthScale : rearLimbLengthScale;
        float comp            = isFront ? frontLimbCompression  : rearLimbCompression;

        Quaternion splayRot = Quaternion.Euler(0, 0, -side * legSplayAngle);

        float compressionRad     = comp * Mathf.Deg2Rad;
        float cosVal             = Mathf.Max(0.1f, Mathf.Abs(Mathf.Cos(compressionRad)));
        float compensationFactor = 1.0f / cosVal;

        Vector3[]    localPositions = new Vector3[6];
        Quaternion[] localRotations = new Quaternion[6];

        localPositions[0] = Vector3.zero;
        localPositions[1] = new Vector3(side * horizontalWidth, 0, 0);
        localRotations[0] = Quaternion.LookRotation(
            localPositions[1] == Vector3.zero ? Vector3.forward : localPositions[1].normalized,
            Vector3.up
        );

        for (int j = 0; j < 4; j++)
        {
            Vector3 baseOffset = GetLegType(j, legType);
            float   scalar     = (j == 0) ? proportions.x : (j == 1 ? proportions.y : (j == 2 ? proportions.z : proportions.w));
            float   lengthComp = (j == 0 || j == 1) ? compensationFactor : 1.0f;
            Vector3 v          = baseOffset * scalar * lScale * globalScale * lengthComp;

            Vector3    segmentVec = Vector3.zero;
            Quaternion segRot     = Quaternion.identity;

            if (j == 0)
            {
                segmentVec = splayRot * (Quaternion.Euler(comp, 0, 0) * v);
                segRot     = segmentVec != Vector3.zero ? Quaternion.LookRotation(segmentVec, splayRot * Vector3.up) : splayRot;
            }
            else if (j == 1)
            {
                segmentVec = splayRot * (Quaternion.Euler(-comp, 0, 0) * v);
                segRot     = segmentVec != Vector3.zero ? Quaternion.LookRotation(segmentVec, splayRot * Vector3.up) : splayRot;
            }
            else if (j == 2)
            {
                if (legType == LegGait.Plantigrade)
                {
                    segmentVec = new Vector3(0, 0, v.magnitude);
                    segRot     = Quaternion.identity;
                }
                else
                {
                    segmentVec = splayRot * v;
                    segRot     = segmentVec != Vector3.zero ? Quaternion.LookRotation(segmentVec, splayRot * Vector3.up) : splayRot;
                }
            }
            else if (j == 3)
            {
                if (legType == LegGait.Plantigrade || legType == LegGait.Digitigrade)
                {
                    segmentVec = new Vector3(0, 0, v.magnitude);
                    segRot     = Quaternion.identity;
                }
                else
                {
                    segmentVec = splayRot * v;
                    segRot     = segmentVec != Vector3.zero ? Quaternion.LookRotation(segmentVec, splayRot * Vector3.up) : splayRot;
                }
            }

            localPositions[j + 2] = localPositions[j + 1] + segmentVec;
            localRotations[j + 1] = segRot;
        }

        localRotations[5] = localRotations[4];

        Quaternion uprightRotation = this.transform.rotation;
        Vector3    worldRootPos    = root.position;

        Transform lastSegment = root;
        for (int i = 0; i < 6; i++)
        {
            string boneName = (i == 0)
                ? (isFront ? "Shoulder_Lateral" : "Pelvis_Lateral")
                : LegBoneNames(i - 1, isFront);

            Vector3    targetWorldPos = worldRootPos + (uprightRotation * localPositions[i]);
            Quaternion targetWorldRot = uprightRotation * localRotations[i];

            Vector3    finalLocalOffset = lastSegment.InverseTransformPoint(targetWorldPos);
            Quaternion finalLocalRot    = Quaternion.Inverse(lastSegment.rotation) * targetWorldRot;

            lastSegment = CreateBone(lastSegment, boneName, finalLocalOffset, finalLocalRot);
        }
    }

    string LegBoneNames(int index, bool isFront)
    {
        string prefix = isFront ? "Front_" : "Rear_";
        switch (index)
        {
            case 0:  return prefix + "Hip";
            case 1:  return prefix + "Knee";
            case 2:  return prefix + "Ankle";
            case 3:  return prefix + "ToeBase";
            case 4:  return prefix + "ToeTip";
            default: return prefix + "LegBone";
        }
    }

    //This is used to determine what type of leg the creature needs
    Vector3 GetLegType(int boneIndex, LegGait legType)
    {
        switch (legType)
        {
            case LegGait.Plantigrade:
                switch (boneIndex)
                {
                    case 0: return new Vector3(0, -0.45f,  0.05f);
                    case 1: return new Vector3(0, -0.45f,  0f);
                    case 2: return new Vector3(0, -0.05f,  0.15f);
                    case 3: return new Vector3(0,  0f,     0.1f);
                    case 4: return new Vector3(0,  0f,     0.05f);
                    default: return Vector3.zero;
                }
            case LegGait.Digitigrade:
                switch (boneIndex)
                {
                    case 0: return new Vector3(0, -0.4f,   0.1f);
                    case 1: return new Vector3(0, -0.4f,  -0.1f);
                    case 2: return new Vector3(0, -0.3f,   0.2f);
                    case 3: return new Vector3(0, -0.05f,  0.1f);
                    case 4: return new Vector3(0,  0f,     0.05f);
                    default: return Vector3.zero;
                }
            case LegGait.Unguligrade:
                switch (boneIndex)
                {
                    case 0: return new Vector3(0, -0.4f,   0.1f);
                    case 1: return new Vector3(0, -0.45f, -0.05f);
                    case 2: return new Vector3(0, -0.5f,   0.05f);
                    case 3: return new Vector3(0, -0.15f,  0.05f);
                    case 4: return new Vector3(0, -0.1f,   0f);
                    default: return Vector3.zero;
                }
            default: return Vector3.zero;
        }
    }

    Transform CreateBone(Transform parent, string boneName, Vector3 offset, Quaternion localRotation)
    {
        Vector3    worldPos      = parent.TransformPoint(offset);
        Quaternion worldRotation = parent.rotation * localRotation;

        GameObject bone = Instantiate(bonePrefab, worldPos, worldRotation, parent);
        bone.name = boneName;
        allBones.Add(bone.transform);
        return bone.transform;
    }
}