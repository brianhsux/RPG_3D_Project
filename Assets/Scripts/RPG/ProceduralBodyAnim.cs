using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight procedural idle / walk / attack for primitive-built characters.
/// No Animator clips required.
/// </summary>
public class ProceduralBodyAnim : MonoBehaviour
{
    public enum BodyStyle
    {
        Auto,
        Humanoid,
        Quadruped,
        Blob,
        Flyer,
        Crawler
    }

    public BodyStyle style = BodyStyle.Auto;

    readonly List<Part> _parts = new List<Part>();
    float _phase;
    float _attackT;
    float _moveAmount;
    float _targetMove;
    bool _dead;

    struct Part
    {
        public Transform t;
        public Vector3 restPos;
        public Quaternion restRot;
        public Vector3 restScale;
        public PartRole role;
    }

    enum PartRole
    {
        Body,
        Head,
        ArmL,
        ArmR,
        LegL,
        LegR,
        WingL,
        WingR,
        Extra
    }

    void Awake()
    {
        CacheParts();
        if (style == BodyStyle.Auto)
            style = DetectStyle();
    }

    void OnEnable()
    {
        // Parts may be rebuilt (armor/enemy respawn) — refresh rest poses next frame.
        Invoke(nameof(RecacheSafe), 0.05f);
    }

    void RecacheSafe()
    {
        if (this == null) return;
        CacheParts();
        if (style == BodyStyle.Auto)
            style = DetectStyle();
    }

    public void Recache()
    {
        CacheParts();
        if (style == BodyStyle.Auto)
            style = DetectStyle();
    }

    public void SetDead(bool dead)
    {
        _dead = dead;
        if (dead) ResetToRest();
    }

    public void SetMoveAmount(float amount01)
    {
        _targetMove = Mathf.Clamp01(amount01);
    }

    public void PlayAttack(float duration = 0.35f)
    {
        _attackT = Mathf.Max(_attackT, duration);
    }

    void CacheParts()
    {
        _parts.Clear();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;

            // Direct children, or one level under ArmorGear / HeroVisual rebuild roots.
            bool direct = child.parent == transform;
            bool underGear = child.parent != null && child.parent.parent == transform &&
                             (child.parent.name == "ArmorGear" || child.parent.name == "HeroVisual");
            if (!direct && !underGear) continue;

            string nm = child.name;
            if (nm == "Sword" || nm == "Hilt" || nm == "ArmorGear" || nm == "HeroVisual" || nm == "ArmorMesh")
                continue;

            PartRole role = Classify(child.name);
            _parts.Add(new Part
            {
                t = child,
                restPos = child.localPosition,
                restRot = child.localRotation,
                restScale = child.localScale,
                role = role
            });
        }
    }

    static PartRole Classify(string name)
    {
        string n = name.ToLowerInvariant();

        if (n == "leftleg" || n == "legl" || n.StartsWith("legl"))
            return PartRole.LegL;
        if (n == "rightleg" || n == "legr" || n.StartsWith("legr"))
            return PartRole.LegR;

        if (n == "leftarm" || n == "arml" || n.StartsWith("arml"))
            return PartRole.ArmL;
        if (n == "rightarm" || n == "armr" || n.StartsWith("armr"))
            return PartRole.ArmR;
        if (n.Contains("wingl") || n == "wingl")
            return PartRole.WingL;
        if (n.Contains("wingr") || n == "wingr")
            return PartRole.WingR;
        if (n.Contains("wing") && n.Contains("l") && !n.Contains("r"))
            return PartRole.WingL;
        if (n.Contains("wing") && n.Contains("r"))
            return PartRole.WingR;

        if (n.Contains("head") || n.Contains("skull") || n.Contains("face") || n.Contains("hood"))
            return PartRole.Head;
        if (n == "cap" || (n.Contains("cap") && !n.Contains("cape") && !n.Contains("capsule")))
            return PartRole.Head;

        if (n.Contains("legfl") || n.Contains("legbl") || n.Contains("legl") ||
            (n.Contains("leg") && n.Contains("l") && !n.Contains("r") && !n.Contains("fr") && !n.Contains("br")))
            return PartRole.LegL;
        if (n.Contains("legfr") || n.Contains("legbr") || n.Contains("legr") ||
            (n.Contains("leg") && n.Contains("r")))
            return PartRole.LegR;

        if (n.Contains("arm") && n.Contains("l") && !n.Contains("r") && !n.Contains("leg"))
            return PartRole.ArmL;
        if (n.Contains("arm") && n.Contains("r") && !n.Contains("leg"))
            return PartRole.ArmR;

        if (n.Contains("body") || n.Contains("torso") || n.Contains("ribs") || n.Contains("shell") ||
            n.Contains("trunk") || n.Contains("robe") || n.Contains("abdomen") || n.Contains("thorax") ||
            n.Contains("stem") || n == "core" || n.Contains("scales") || n.Contains("darkplate") ||
            n.Contains("breastplate") || n.Contains("plate") && n.Contains("breast") == false && n == "plate")
            return PartRole.Body;

        return PartRole.Extra;
    }

    BodyStyle DetectStyle()
    {
        bool hasLegs = false, hasArms = false, hasWings = false, hasBody = false;
        int legCount = 0;
        foreach (var p in _parts)
        {
            if (p.role == PartRole.LegL || p.role == PartRole.LegR) { hasLegs = true; legCount++; }
            if (p.role == PartRole.ArmL || p.role == PartRole.ArmR) hasArms = true;
            if (p.role == PartRole.WingL || p.role == PartRole.WingR) hasWings = true;
            if (p.role == PartRole.Body) hasBody = true;
        }
        if (hasWings) return BodyStyle.Flyer;
        if (legCount >= 2 && !hasArms) return BodyStyle.Quadruped;
        if (hasArms || hasLegs) return BodyStyle.Humanoid;
        if (hasBody) return BodyStyle.Blob;
        return BodyStyle.Crawler;
    }

    void LateUpdate()
    {
        if (_dead) return;
        if (_parts.Count == 0) CacheParts();

        _moveAmount = Mathf.MoveTowards(_moveAmount, _targetMove, Time.deltaTime * 6f);
        float walkSpeed = Mathf.Lerp(1.2f, 9f, _moveAmount);
        _phase += Time.deltaTime * walkSpeed;

        if (_attackT > 0f)
            _attackT -= Time.deltaTime;

        float attack = _attackT > 0f ? Mathf.Clamp01(_attackT / 0.35f) : 0f;
        // Attack curve: wind-up then strike
        float strike = attack > 0f ? Mathf.Sin((1f - attack) * Mathf.PI) : 0f;

        foreach (var p in _parts)
        {
            if (p.t == null) continue;
            AnimatePart(p, strike);
        }
    }

    void AnimatePart(Part p, float strike)
    {
        Vector3 pos = p.restPos;
        Quaternion rot = p.restRot;
        Vector3 scale = p.restScale;

        float idle = 1f - _moveAmount;
        float walk = _moveAmount;
        float bob = Mathf.Sin(_phase) * 0.04f * walk + Mathf.Sin(Time.time * 2.2f) * 0.015f * idle;
        float breath = 1f + Mathf.Sin(Time.time * 2.5f) * 0.03f * idle;

        switch (style)
        {
            case BodyStyle.Blob:
                AnimateBlob(ref pos, ref rot, ref scale, p, bob, breath, walk, strike);
                break;
            case BodyStyle.Flyer:
                AnimateFlyer(ref pos, ref rot, ref scale, p, bob, breath, walk, strike);
                break;
            case BodyStyle.Quadruped:
                AnimateQuad(ref pos, ref rot, ref scale, p, bob, breath, walk, strike);
                break;
            case BodyStyle.Crawler:
                AnimateCrawler(ref pos, ref rot, ref scale, p, bob, breath, walk, strike);
                break;
            default:
                AnimateHumanoid(ref pos, ref rot, ref scale, p, bob, breath, walk, strike);
                break;
        }

        p.t.localPosition = pos;
        p.t.localRotation = rot;
        p.t.localScale = scale;
    }

    void AnimateHumanoid(ref Vector3 pos, ref Quaternion rot, ref Vector3 scale, Part p,
        float bob, float breath, float walk, float strike)
    {
        float swing = Mathf.Sin(_phase) * 28f * walk;
        float armSwing = Mathf.Sin(_phase) * 35f * walk;

        switch (p.role)
        {
            case PartRole.Body:
                pos.y += bob * 0.35f;
                scale = Vector3.Scale(p.restScale, new Vector3(breath, 1f / Mathf.Lerp(1f, breath, 0.5f), breath));
                rot = p.restRot * Quaternion.Euler(strike * 18f, 0f, Mathf.Sin(Time.time * 1.5f) * 2f * (1f - walk));
                break;
            case PartRole.Head:
                pos.y += bob * 0.2f;
                rot = p.restRot * Quaternion.Euler(Mathf.Sin(_phase * 0.5f) * 6f * walk + strike * -10f, 0f, 0f);
                break;
            case PartRole.ArmL:
                rot = p.restRot * Quaternion.Euler(armSwing + strike * -50f, 0f, 8f + walk * 5f);
                break;
            case PartRole.ArmR:
                // Main attack arm
                rot = p.restRot * Quaternion.Euler(-armSwing + strike * -70f, strike * 25f, -8f - walk * 5f);
                break;
            case PartRole.LegL:
                rot = p.restRot * Quaternion.Euler(-swing, 0f, 0f);
                pos.y += Mathf.Max(0f, Mathf.Sin(_phase) * 0.03f * walk);
                break;
            case PartRole.LegR:
                rot = p.restRot * Quaternion.Euler(swing, 0f, 0f);
                pos.y += Mathf.Max(0f, Mathf.Sin(_phase + Mathf.PI) * 0.03f * walk);
                break;
            case PartRole.WingL:
                rot = p.restRot * Quaternion.Euler(0f, 0f, 15f + Mathf.Sin(_phase * 1.5f) * 25f);
                break;
            case PartRole.WingR:
                rot = p.restRot * Quaternion.Euler(0f, 0f, -15f - Mathf.Sin(_phase * 1.5f) * 25f);
                break;
            default:
                pos.y += bob * 0.1f;
                break;
        }
    }

    void AnimateQuad(ref Vector3 pos, ref Quaternion rot, ref Vector3 scale, Part p,
        float bob, float breath, float walk, float strike)
    {
        float swing = Mathf.Sin(_phase) * 30f * walk;
        switch (p.role)
        {
            case PartRole.Body:
                pos.y += bob * 0.5f;
                rot = p.restRot * Quaternion.Euler(strike * 12f, 0f, Mathf.Sin(_phase) * 3f * walk);
                scale = Vector3.Scale(p.restScale, new Vector3(breath, 1f, breath));
                break;
            case PartRole.Head:
                rot = p.restRot * Quaternion.Euler(strike * 20f, Mathf.Sin(Time.time * 2f) * 4f * (1f - walk), 0f);
                break;
            case PartRole.LegL:
                rot = p.restRot * Quaternion.Euler(-swing, 0f, 0f);
                break;
            case PartRole.LegR:
                rot = p.restRot * Quaternion.Euler(swing, 0f, 0f);
                break;
            default:
                // Tail / extras
                if (p.t.name.ToLowerInvariant().Contains("tail"))
                    rot = p.restRot * Quaternion.Euler(0f, Mathf.Sin(_phase) * 20f * walk + Mathf.Sin(Time.time * 2f) * 8f * (1f - walk), 0f);
                else
                    pos.y += bob * 0.15f;
                break;
        }
    }

    void AnimateBlob(ref Vector3 pos, ref Quaternion rot, ref Vector3 scale, Part p,
        float bob, float breath, float walk, float strike)
    {
        float squash = 1f + Mathf.Sin(_phase) * 0.12f * walk;
        float stretch = 1f / Mathf.Sqrt(Mathf.Max(0.5f, squash));
        if (p.role == PartRole.Body || p.role == PartRole.Extra && p.t.name.Contains("Body"))
        {
            scale = Vector3.Scale(p.restScale, new Vector3(squash * breath, stretch / breath, squash * breath));
            pos.y += bob * 0.6f + strike * 0.15f;
            rot = p.restRot * Quaternion.Euler(strike * 25f, 0f, Mathf.Sin(_phase) * 6f * walk);
        }
        else if (p.role == PartRole.Head || p.t.name.ToLowerInvariant().Contains("eye"))
        {
            pos.y += bob * 0.4f;
            scale = Vector3.Scale(p.restScale, Vector3.one * (1f + strike * 0.15f));
        }
        else
        {
            pos.y += bob * 0.2f;
        }
    }

    void AnimateFlyer(ref Vector3 pos, ref Quaternion rot, ref Vector3 scale, Part p,
        float bob, float breath, float walk, float strike)
    {
        float flapSpeed = Mathf.Lerp(3f, 10f, walk);
        float flap = Mathf.Sin(Time.time * flapSpeed) * Mathf.Lerp(18f, 40f, walk);
        switch (p.role)
        {
            case PartRole.Body:
                pos.y += Mathf.Sin(Time.time * 2.5f) * 0.06f + bob;
                rot = p.restRot * Quaternion.Euler(strike * 20f, 0f, 0f);
                break;
            case PartRole.WingL:
                rot = p.restRot * Quaternion.Euler(0f, 0f, 20f + flap);
                break;
            case PartRole.WingR:
                rot = p.restRot * Quaternion.Euler(0f, 0f, -20f - flap);
                break;
            case PartRole.Head:
                rot = p.restRot * Quaternion.Euler(strike * 15f, 0f, 0f);
                break;
            default:
                pos.y += Mathf.Sin(Time.time * 2.5f) * 0.03f;
                break;
        }
    }

    void AnimateCrawler(ref Vector3 pos, ref Quaternion rot, ref Vector3 scale, Part p,
        float bob, float breath, float walk, float strike)
    {
        float skitter = Mathf.Sin(_phase + p.restPos.x * 4f) * 18f * walk;
        if (p.role == PartRole.LegL || p.role == PartRole.LegR || p.t.name.ToLowerInvariant().Contains("leg"))
        {
            rot = p.restRot * Quaternion.Euler(skitter, 0f, skitter * 0.5f);
        }
        else if (p.role == PartRole.Body)
        {
            pos.y += bob * 0.4f;
            rot = p.restRot * Quaternion.Euler(strike * 15f, 0f, Mathf.Sin(_phase) * 4f * walk);
            scale = Vector3.Scale(p.restScale, new Vector3(breath, 1f, breath));
        }
        else
        {
            pos.y += bob * 0.15f;
        }
    }

    void ResetToRest()
    {
        foreach (var p in _parts)
        {
            if (p.t == null) continue;
            p.t.localPosition = p.restPos;
            p.t.localRotation = p.restRot;
            p.t.localScale = p.restScale;
        }
    }
}
