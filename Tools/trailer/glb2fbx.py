import bpy, sys, os
argv = sys.argv[sys.argv.index("--")+1:]
src, fbx_out, tex_dir = argv[0], argv[1], argv[2]
os.makedirs(tex_dir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
# dump every embedded texture so we have a fallback path that needs no FBX import
for img in bpy.data.images:
    if img.size[0] == 0:
        continue
    safe = "".join(c for c in img.name if c.isalnum() or c in "._-")
    img.filepath_raw = os.path.join(tex_dir, safe + ".png")
    img.file_format = 'PNG'
    try:
        img.save()
        print("TEX", img.filepath_raw, img.size[0], img.size[1])
    except Exception as e:
        print("TEXFAIL", img.name, e)
bpy.ops.export_scene.fbx(filepath=fbx_out, path_mode='COPY', embed_textures=True)
print("FBX", fbx_out)
