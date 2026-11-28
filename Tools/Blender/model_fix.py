# ФИКС ИМПОРТИРОВАННЫХ МОДЕЛЕЙ В BLENDER (гнолл/крыса/волк и любые glb из ветки)
#
# ЗАПУСК:
#   1. Blender: File -> Import -> glTF 2.0 -> выбрать Gnoll.glb (или перетащить glb в окно 3D).
#   2. Вкладка Scripting -> Open -> выбрать этот файл -> Run Script (кнопка со стрелкой).
#   3. Читать вывод в консоли/логе: строки [МОДЕЛЬ-ФИКС].
#   4. Экспорт обратно: File -> Export -> glTF 2.0 (галка +Y Up по умолчанию).
#
# Что делает: пересчитывает нормали наружу (чёрные пятна), включает сглаживание
# с автосмягчением по углу (~50 градусов), ставит модель на землю (низ равен Z=0).
# Опционально сам экспортирует glb, если заполнить EXPORT_PATH ниже.

import bpy
import math

PREFIXES = ("Gnoll", "Rat", "Wolf", "Body", "Spear")
EXPORT_PATH = ""

LOG = "[МОДЕЛЬ-ФИКС]"


def log(msg):
    print(LOG, msg)


def is_target(o):
    return o.type == 'MESH' and (not PREFIXES or o.name.startswith(PREFIXES))


def recalc_normals(o):
    me = o.data
    if not me.polygons:
        log("пропуск '%s': у меша нет полигонов" % o.name)
        return
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    log("'%s': нормали пересчитаны наружу" % o.name)


def smooth(o):
    bpy.ops.object.shade_smooth()
    me = o.data
    if hasattr(me, "use_auto_smooth"):
        me.use_auto_smooth = True
        me.auto_smooth_angle = math.radians(50.0)
        log("'%s': shade_smooth + auto_smooth 50° (Blender 3.x)" % o.name)
    else:
        try:
            bpy.ops.object.shade_auto_smooth(angle=math.radians(50.0))
            log("'%s': shade_auto_smooth 50° (Blender 4.x)" % o.name)
        except Exception as e:
            log("'%s': auto_smooth недоступен, оставлен простой smooth — причина: %s" % (o.name, e))


def snap_to_ground(o):
    me = o.data
    mw = o.matrix_world
    minz = min((mw @ v.co).z for v in me.vertices)
    o.location.z -= minz
    log("'%s': опущен на землю на %.4f" % (o.name, minz))


def main():
    targets = [o for o in bpy.context.scene.objects if is_target(o)]
    if not targets:
        log("ОТКАЗ: в сцене нет мешей с префиксами %s. Импортируй glb перед запуском." % (PREFIXES,))
        return
    for o in bpy.context.scene.objects:
        o.select_set(False)
    log("целей: %d -> %s" % (len(targets), [o.name for o in targets]))
    for o in targets:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        try:
            recalc_normals(o)
            smooth(o)
            snap_to_ground(o)
        except Exception as e:
            log("ОТКАЗ на '%s' — причина: %r" % (o.name, e))
        o.select_set(False)
    if EXPORT_PATH:
        try:
            bpy.ops.export_scene.gltf(filepath=EXPORT_PATH, export_format='GLB', use_selection=False)
            log("экспорт выполнен: %s" % EXPORT_PATH)
        except Exception as e:
            log("ОТКАЗ экспорта — причина: %r" % e)
    log("готово.")


main()
