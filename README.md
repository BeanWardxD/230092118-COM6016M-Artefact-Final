How to use this example of the ecologically plausible creatures generator:

1. Assign your values in the EcoInputGen component.
2. Press the 3 dots to the right of the components head.
3. Look to the bottom of that menu and select "evolve" .
4. A creature will be created.
5. You can make a new one in the same way.
6. If you want to see how the inputs effect the generation both the mesh generator
   and the skeleton generator have their own options to generate their part independently
   with the appropriate controls expose in the inspector.

How to setup the ecologically plausible creatures generator:

1. You need to apply EcoInputGen, MeshGen and SkelGen as components to an empty object.
2. In EcoInputGen set your empty in the generator references.
3. In SkelGen set the bone prefab to 'Head' under assets. Optionaly add a bone renderer for it.
4. The scripts arent set up for two different creatures at the moment and will steal eachothers meshes.
