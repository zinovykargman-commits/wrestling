using UnityEngine;

// Explicit diagnostic side view only; ordinary gameplay never creates this.
[DefaultExecutionOrder(1000)]
public class GBAuditShotCamera:MonoBehaviour {
    public GBWrestler Wrestler;
    public Camera View;
    public bool Capturing;
    void LateUpdate(){
        if(!Capturing||!GBApp.I.Automated||!Wrestler||!View)return;
        var hips=Wrestler.Root.position;
        View.fieldOfView=46;View.transform.position=hips+new Vector3(3.2f,.75f,-.35f);
        View.transform.LookAt(hips+Vector3.up*.30f);
    }
}
