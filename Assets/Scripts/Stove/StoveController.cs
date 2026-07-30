using UnityEngine;

public class StoveController : MonoBehaviour
{
    [Header("Ручки управления горелок (Burner Knobs)")]
    public StoveKnob burnerKnob1;
    public StoveKnob burnerKnob2;
    public StoveKnob burnerKnob3;
    public StoveKnob burnerKnob4;

    [Header("Ручка духовки (Oven Knob)")]
    public StoveKnob ovenKnob;

    public StoveKnob GetBurnerKnob(int index)
    {
        switch (index)
        {
            case 0: return burnerKnob1;
            case 1: return burnerKnob2;
            case 2: return burnerKnob3;
            case 3: return burnerKnob4;
            default: return null;
        }
    }

    public bool IsBurnerOn(int index)
    {
        StoveKnob knob = GetBurnerKnob(index);
        return knob != null && knob.isOn;
    }

    public bool IsOvenOn()
    {
        return ovenKnob != null && ovenKnob.isOn;
    }
}