# ModCommon
A small dependency for Mod Developers that does the bare minimum, that can be installed alongside R2API.

The entire feature list:
 - Sets the isModded flag for RoR2Application.
 - Adds mods without the exception attribute to the network modlist (R2API will handle this when installed, the exception attribute from this library will still work with R2API).
 - Adds a patch called ILLine that helps mod developers to diagnose errors faster.
 - Patch to fix duplication of console messages.
That's it.

## I want to use this and don't want to be in the network list
Add a reference to ModCommon and add `[ModCommon.NetworkModlistException]` next to your BepInPlugin attribute.

You can also add an attribute you make yourself called `ManualNetworkRegistrationAttribute` to your assembly, making the namespace `R2API.Utils.ManualNetworkRegistrationAttribute` will also exclude it from R2API.