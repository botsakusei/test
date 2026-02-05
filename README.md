# Unity Weapon Equip System

## Overview

This is a Unity weapon equipping system that provides a smooth drawing and sheathing animation system for weapons (e.g., swords). The system allows weapons to smoothly transition between hand and sheath positions with customizable animation paths.

## Features

- **WeaponEquipWizard**: Unity Editor window for setting up weapon anchors and draw paths
- **WeaponBladeFollower**: Runtime component that handles weapon position and animation transitions
- Customizable draw/sheath animations with multiple waypoints
- Support for both right and left hand configurations
- Configurable sheath locations (Hips, Spine)
- Preview mode for testing animations in the editor

## Prerequisites

- Unity Editor (2019.4 or later recommended)
- A rigged character model with appropriate bone structure
- A weapon GameObject that you want to equip/unequip

## How to Use

### Setup

1. **Open the Weapon Equip Wizard**
   - In Unity Editor, go to `Tools > Weapon Equip Wizard`

2. **Select your weapon GameObject** in the hierarchy

3. **Configure settings in the wizard:**
   - Choose hand side (Right/Left)
   - Choose sheath location (Hips/Spine)
   - Set blend time for smooth transitions
   - Optionally add draw points for custom animation paths

4. **Click "Create / Update"** to generate the anchor structure

### Runtime Usage

The `WeaponBladeFollower` component will be automatically added to your weapon. You can:

- Call `Toggle()` to switch between equipped/sheathed states
- Use `IsEquipped` property to check current state
- Adjust `BlendTime` to control transition speed
- Set up draw points for custom animation paths

### Preview

Enable `PreviewMode` on the WeaponBladeFollower component to test the animation in edit mode using the `PreviewT` slider (0 = sheathed, 1 = equipped).

## File Structure

- `Assets_Editor_Weapon_WeaponEquipWizard_Version2.cs` - Editor tool for setting up weapon anchors
- `Assets_Scripts_Weapon_WeaponBladeFollower_Version2.cs` - Runtime script for weapon animation
