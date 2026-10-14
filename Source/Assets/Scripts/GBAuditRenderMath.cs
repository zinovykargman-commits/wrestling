using UnityEngine;
using System.Collections;

public partial class GBAudit {
    IEnumerator RenderMathRun(){
        var fixture=new GameObject("transform-equivalence-fixture");var t=fixture.transform;
        float pointError=0,directionError=0;
        for(int n=0;n<64;n++){
            t.SetPositionAndRotation(new Vector3(n*.13f,-n*.07f,n*.19f),Quaternion.Euler(n*11,n*17,n*23));
            t.localScale=new Vector3(.7f+n*.01f,1.4f-n*.005f,1.1f+n*.003f);
            var p=new Vector3(Mathf.Sin(n),Mathf.Cos(n*.3f),Mathf.Sin(n*.7f));
            var world=t.TransformPoint(p);
            pointError=Mathf.Max(pointError,Vector3.Distance(world,t.localToWorldMatrix.MultiplyPoint3x4(p)));
            pointError=Mathf.Max(pointError,Vector3.Distance(t.InverseTransformPoint(world),t.worldToLocalMatrix.MultiplyPoint3x4(world)));
            directionError=Mathf.Max(directionError,Vector3.Distance(t.InverseTransformDirection(p),Quaternion.Inverse(t.rotation)*p));
        }
        Check("cached_transform_math_preserves_point_and_direction",pointError<.00001f&&directionError<.00001f,"64 translated/rotated/nonuniform-scaled cases; max point error="+pointError+"m; direction="+directionError+"; no physics/mesh/sample change");
        Destroy(fixture);yield return null;
    }
}
