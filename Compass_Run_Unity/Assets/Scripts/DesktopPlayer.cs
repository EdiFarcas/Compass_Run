using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class DesktopPlayer : MonoBehaviour
{
    public Camera view;
    public float speed = 3.5f;
    public float reach = 3f;
    CharacterController controller;
    float pitch, vertical;
    Rigidbody held;
    bool previousGravity, previousKinematic;
    CollisionDetectionMode previousDetection;
    DesktopPickup target;
    Vector3 holdOffset;

    void Start() { controller = GetComponent<CharacterController>(); pitch = 25; Lock(true); }
    void Lock(bool value) { Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !value; }
    void Update()
    {
        var k = Keyboard.current; var m = Mouse.current;
        if(k == null || m == null || view == null) return;
        if(k.escapeKey.wasPressedThisFrame) { Lock(false); return; }
        if(Cursor.lockState != CursorLockMode.Locked) { if(m.leftButton.wasPressedThisFrame) Lock(true); return; }
        var delta = m.delta.ReadValue() * .09f;
        transform.Rotate(0, delta.x, 0);
        pitch = Mathf.Clamp(pitch-delta.y, -80, 80); view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        Vector2 move = Vector2.zero;
        if(k.wKey.isPressed) move.y++; if(k.sKey.isPressed) move.y--;
        if(k.dKey.isPressed) move.x++; if(k.aKey.isPressed) move.x--;
        move = Vector2.ClampMagnitude(move, 1);
        if(controller.isGrounded && vertical < 0) vertical = -2;
        vertical += Physics.gravity.y * Time.deltaTime;
        controller.Move(((transform.forward * move.y + transform.right * move.x) * speed + Vector3.up * vertical) * Time.deltaTime);
        target = null;
        if(!held && Physics.Raycast(view.transform.position, view.transform.forward, out var hit, reach, ~0, QueryTriggerInteraction.Ignore)) target = hit.collider.GetComponentInParent<DesktopPickup>();
        if(m.leftButton.wasPressedThisFrame || k.eKey.wasPressedThisFrame)
        {
            if(held) Release(); else if(target) Grab(target);
        }
        if(held && m.rightButton.wasPressedThisFrame) { var body = held; Release(); body.AddForce(view.transform.forward * 3, ForceMode.VelocityChange); }
    }
    void Grab(DesktopPickup pickup)
    {
        held = pickup.GetComponent<Rigidbody>(); previousGravity = held.useGravity; previousKinematic = held.isKinematic; previousDetection = held.collisionDetectionMode;
        held.linearVelocity = Vector3.zero; held.angularVelocity = Vector3.zero;
        held.collisionDetectionMode = CollisionDetectionMode.Discrete; held.isKinematic = true; held.useGravity = false;
        var renderers = held.GetComponentsInChildren<Renderer>(); var bounds = new Bounds(held.position, Vector3.zero);
        if(renderers.Length > 0) { bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds); }
        holdOffset = held.position - bounds.center;
        foreach(var collider in held.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(controller, collider, true);
    }
    void FixedUpdate()
    {
        if(held) held.MovePosition(view.transform.position + view.transform.forward * 1.1f - view.transform.up * .12f + holdOffset);
    }
    void Release()
    {
        if(!held) return;
        foreach(var collider in held.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(controller, collider, false);
        held.isKinematic = previousKinematic; held.useGravity = previousGravity; held.collisionDetectionMode = previousDetection; held = null;
    }
    void OnDisable() { Release(); Lock(false); }
    void OnGUI()
    {
        GUI.Label(new Rect(Screen.width/2f-6, Screen.height/2f-12, 24, 24), "+");
        GUI.Box(new Rect(12,12,Mathf.Min(590, Screen.width-24),58), "WASD: deplasare | Mouse: privire | Esc: cursor liber\nClick / E: apuca sau elibereaza | Click dreapta: arunca");
        if(target) GUI.Box(new Rect(Screen.width/2f-150, Screen.height/2f+30,300,30), "Click / E: " + target.displayName);
        if(held) GUI.Box(new Rect(Screen.width/2f-150,Screen.height/2f+30,300,30), "Click / E: elibereaza | Dreapta: arunca");
    }
}
