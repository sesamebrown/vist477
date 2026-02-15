# Swimming Locomotion System

VR swimming locomotion for Unity XR Interaction Toolkit. Swim through space by pulling yourself with controller movements.

## Quick Start

1. **Attach** `SwimLocomotionController` to your XR Origin GameObject
2. **Disable gravity** on DynamicMoveProvider and GravityProvider components (uncheck "Use Gravity")
3. **Test in VR**: Hold side grip buttons and make swimming motions
4. **Tune** settings in Inspector if needed (defaults work well for most cases)

## How It Works

Momentum-based physics model:
- Hand movements generate propulsion forces (opposite direction = Newton's 3rd law)
- Forces accumulate into velocity that persists and decays naturally
- Creates smooth "stroke → glide → slow stop" cycle
- Forces calculated relative to XR Origin rotation (works with snap turns)

**The "Return Stroke" Problem**: Without mitigation, bringing hands forward after a stroke would push you backward. Solution: hold grip button during power stroke, release during return.

## Settings Overview

### Essential Settings
- **requireGripPress** (default: true) - Must hold grip button to generate force
- **gravityScale** (default: 0) - Set to 0 for weightless air swimming
- **forceMultiplier** (default: 10) - How fast you swim
- **dragCoefficient** (default: 0.015) - How quickly you slow down

### Advanced Settings
- **minimumSpeed** - Minimum hand speed to generate force
- **maxForcePerFrame** - Caps acceleration to prevent extreme speeds
- **forceSmoothing** - Reduces jittery movement
- **stopThreshold** - Prevents infinite drifting
- **considerHandRotation** - Palm orientation affects force (cupped vs slicing)
- **requireBothHands** - Only swim when both hands moving

## Tuning Guide

**Longer gliding**: Decrease `dragCoefficient` (0.005-0.01)  
**Faster stop**: Increase `dragCoefficient` (0.03-0.05)  
**Faster swimming**: Increase `forceMultiplier` (15-30)  
**More control**: Increase `minimumSpeed` (0.15-0.2)  
**Add gravity**: Increase `gravityScale` (1-5 for sinking, -1 for buoyancy)


## Debug Mode

Enable `debugMode` in Inspector to visualize:
- **Cyan ray** from camera - Swimming velocity direction
- **Red/Green rays** from controllers - Hand velocities
- **Blue rays** - Palm directions (when `considerHandRotation` enabled)
- **Console logs** - Detailed movement data

## Troubleshooting

| Issue | Solution |
|-------|----------|
| No movement | Enable debug mode, verify grip buttons pressed, check `minimumSpeed` not too high |
| Falling down | Set `gravityScale = 0`, disable gravity on DynamicMoveProvider & GravityProvider |
| Too fast/slow | Adjust `forceMultiplier` (10-30 range) |
| Stops too abruptly | Decrease `dragCoefficient` (try 0.01 or lower) |
| Too much momentum | Increase `dragCoefficient` (try 0.03-0.05) |
| Jittery movement | Increase `forceSmoothing`, check controller tracking |
| Wrong direction after snap turn | Should work automatically (forces are calculated relative to XR Origin rotation) |

## API Reference

```csharp
// Enable/disable swimming
controller.IsEnabled = true/false;

// Stop all motion immediately  
controller.StopSwimming();

// Read current velocity
Vector3 velocity = controller.CurrentVelocity;

// Access hand data (advanced)
HandSwimData leftData = controller.GetLeftHandData();
HandSwimData rightData = controller.GetRightHandData();
```

## Technical Notes

- Uses Unity's built-in XR Input System (`CommonUsages.gripButton`, `devicePosition`, `deviceRotation`)
- Lightweight - single Update() loop, no physics engine
- Works with or without CharacterController
- Forces calculated in XR Origin local space for snap turn compatibility
- Default drag coefficient (0.015) provides ~3 second glide at 60fps

---

**Unity Version**: 2022.3+  
**XR Interaction Toolkit**: 3.3.1  
