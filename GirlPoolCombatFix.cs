using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using BepInEx;
using BepInEx.Logging;

using HarmonyLib;

using GAT;


[BepInPlugin(
    "benja.girlpool.combatfix",
    "GirlPool Combat Fix",
    "1.0.0"
)]
[BepInDependency(
    "fuwuvi.CustomModelsLoader",
    BepInDependency.DependencyFlags.HardDependency
)]
public sealed class GirlPoolCombatFixPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    private Harmony _harmony;


    private void Awake()
    {
        Log = Logger;

        _harmony = new Harmony(
            "benja.girlpool.combatfix"
        );

        _harmony.PatchAll();

        Logger.LogInfo(
            "GirlPool Combat Fix loaded"
        );
    }
}


// ============================================================
// PATCH GATCombatBaseBehavior.Attack
// ============================================================

[HarmonyPatch(
    typeof(GATCombatBaseBehavior),
    nameof(GATCombatBaseBehavior.Attack)
)]
internal static class GATCombatBaseBehavior_Attack_Patch
{
    private static readonly MethodInfo
        GetTemplateIndexFromHandleMethod =
            AccessTools.Method(
                typeof(GAT.GAT),
                nameof(
                    GAT.GAT.GetTemplateIndexFromHandle
                )
            );


    private static readonly MethodInfo
        CompatMethod =
            AccessTools.Method(
                typeof(
                    GATCombatBaseBehavior_Attack_Patch
                ),
                nameof(
                    GetTemplateIndexForCombatComparison
                )
            );


    // ========================================================
    // TRANSPILER
    //
    // ORIGINAL:
    //
    // GetTemplateIndexFromHandle(handle)
    // ldarg.3
    // bne.un ...
    //
    // NUEVO:
    //
    // GetTemplateIndexForCombatComparison(
    //     gat,
    //     handle,
    //     templateIndex
    // )
    // ldarg.3
    // bne.un ...
    // ========================================================

    private static IEnumerable<CodeInstruction>
        Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
    {
        List<CodeInstruction> code =
            new List<CodeInstruction>(
                instructions
            );


        int replaced = 0;


        for (
            int i = 0;
            i < code.Count;
            i++
        )
        {
            CodeInstruction instruction =
                code[i];


            if (
                instruction.Calls(
                    GetTemplateIndexFromHandleMethod
                )
            )
            {
                // Queremos específicamente la llamada
                // cuya siguiente instrucción compara
                // contra arg.3 = templateIndex.

                bool followedByTemplateIndex =
                    i + 1 < code.Count
                    &&
                    code[i + 1].opcode
                        == OpCodes.Ldarg_3;


                if (followedByTemplateIndex)
                {
                    // Antes de esta instrucción el stack
                    // ya contiene:
                    //
                    // GAT
                    // Handle
                    //
                    // Añadimos templateIndex y llamamos
                    // nuestro método compatible.

                    yield return new CodeInstruction(
                        OpCodes.Ldarg_3
                    );


                    yield return new CodeInstruction(
                        OpCodes.Call,
                        CompatMethod
                    );


                    replaced++;

                    continue;
                }
            }


            yield return instruction;
        }


        GirlPoolCombatFixPlugin.Log?.LogInfo(
            "[GirlPoolCombatFix] "
            + "Attack comparisons patched: "
            + replaced
        );
    }


    // ========================================================
    // COMPARACIÓN COMPATIBLE
    // ============================================================

    private static int
        GetTemplateIndexForCombatComparison(
            GAT.GAT gat,
            Handle handle,
            int requestedTemplateIndex
        )
    {
        int actualTemplateIndex =
            gat.GetTemplateIndexFromHandle(
                handle
            );


        // Comportamiento normal.
        if (
            actualTemplateIndex
            == requestedTemplateIndex
        )
        {
            return actualTemplateIndex;
        }


        if (actualTemplateIndex < 0)
        {
            return actualTemplateIndex;
        }


        try
        {
            Template actualTemplate =
                gat.GetTemplate(
                    actualTemplateIndex
                );


            Template requestedTemplate =
                gat.GetTemplate(
                    requestedTemplateIndex
                );


            // Solo relajamos la comparación si
            // AMBOS pertenecen a las wolf girls.
            if (
                GirlTemplateDetector.IsGirlTemplate(
                    actualTemplate
                )
                &&
                GirlTemplateDetector.IsGirlTemplate(
                    requestedTemplate
                )
            )
            {
                GirlPoolCombatFixPlugin.Log?.LogInfo(
                    "[GirlPoolCombatFix] "
                    + "Allowed custom girl attack | "
                    + "actual="
                    + actualTemplateIndex
                    + " requested="
                    + requestedTemplateIndex
                );


                // Devolvemos el índice que Attack()
                // espera comparar.
                return requestedTemplateIndex;
            }
        }
        catch (Exception ex)
        {
            GirlPoolCombatFixPlugin.Log?.LogError(
                "[GirlPoolCombatFix] "
                + "comparison error:\n"
                + ex
            );
        }


        // Para cualquier otro animal/template:
        // comportamiento vanilla intacto.
        return actualTemplateIndex;
    }
}


// ============================================================
// DETECTOR DE TEMPLATES GIRLPOOL
//
// No resolvemos CustomModelsLoader durante el inicio.
// Se resuelve cuando realmente ocurre un ataque,
// cuando el mod ya está cargado.
// ============================================================

internal static class GirlTemplateDetector
{
    private static bool _resolved;

    private static Type _girlPoolType;

    private static MethodInfo _isGirlTemplateMethod;

    private static FieldInfo _vanillaTemplateField;

    private static FieldInfo _slotsField;


    private static void Resolve()
    {
        if (_resolved)
            return;


        _resolved = true;


        _girlPoolType =
            AccessTools.TypeByName(
                "CustomModelsLoader.src.Features.GirlPool"
            );


        if (_girlPoolType == null)
        {
            GirlPoolCombatFixPlugin.Log?.LogWarning(
                "[GirlPoolCombatFix] "
                + "GirlPool type not found"
            );

            return;
        }


        _isGirlTemplateMethod =
            AccessTools.Method(
                _girlPoolType,
                "IsGirlTemplate"
            );


        _vanillaTemplateField =
            AccessTools.Field(
                _girlPoolType,
                "_vanillaTemplate"
            );


        _slotsField =
            AccessTools.Field(
                _girlPoolType,
                "_slots"
            );


        GirlPoolCombatFixPlugin.Log?.LogInfo(
            "[GirlPoolCombatFix] "
            + "GirlPool reflection ready"
        );
    }


    public static bool IsGirlTemplate(
        Template template
    )
    {
        if (template == null)
            return false;


        // ====================================================
        // Vanilla worker template
        // ====================================================

        try
        {
            Template commandTemplate =
                World.commandService
                    ?.CommandTemplate;


            if (
                commandTemplate != null
                &&
                ReferenceEquals(
                    template,
                    commandTemplate
                )
            )
            {
                return true;
            }
        }
        catch
        {
        }


        Resolve();


        if (_girlPoolType == null)
        {
            return false;
        }


        // ====================================================
        // Si CustomModelsLoader posee IsGirlTemplate,
        // usamos directamente su propia lógica.
        // ====================================================

        if (_isGirlTemplateMethod != null)
        {
            try
            {
                object result =
                    _isGirlTemplateMethod.Invoke(
                        null,
                        new object[]
                        {
                            template
                        }
                    );


                if (
                    result is bool value
                    &&
                    value
                )
                {
                    return true;
                }
            }
            catch
            {
            }
        }


        // ====================================================
        // Vanilla template interno del GirlPool
        // ====================================================

        if (_vanillaTemplateField != null)
        {
            try
            {
                Template vanilla =
                    _vanillaTemplateField
                        .GetValue(null)
                    as Template;


                if (
                    vanilla != null
                    &&
                    ReferenceEquals(
                        vanilla,
                        template
                    )
                )
                {
                    return true;
                }
            }
            catch
            {
            }
        }


        // ====================================================
        // Buscar dentro de _slots
        // ====================================================

        if (_slotsField != null)
        {
            try
            {
                object slots =
                    _slotsField.GetValue(
                        null
                    );


                if (
                    slots is IEnumerable enumerable
                )
                {
                    foreach (
                        object slot
                        in enumerable
                    )
                    {
                        if (
                            ContainsTemplate(
                                slot,
                                template
                            )
                        )
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }
        }


        return false;
    }


    // ========================================================
    // El slot puede ser:
    //
    // Template
    // o una clase/struct que contenga Template.
    // ========================================================

    private static bool ContainsTemplate(
        object item,
        Template wanted
    )
    {
        if (item == null)
            return false;


        if (
            item is Template direct
        )
        {
            return ReferenceEquals(
                direct,
                wanted
            );
        }


        Type type =
            item.GetType();


        foreach (
            FieldInfo field
            in type.GetFields(
                BindingFlags.Instance
                |
                BindingFlags.Public
                |
                BindingFlags.NonPublic
            )
        )
        {
            if (
                typeof(Template)
                .IsAssignableFrom(
                    field.FieldType
                )
            )
            {
                Template value =
                    field.GetValue(
                        item
                    )
                    as Template;


                if (
                    ReferenceEquals(
                        value,
                        wanted
                    )
                )
                {
                    return true;
                }
            }
        }


        foreach (
            PropertyInfo property
            in type.GetProperties(
                BindingFlags.Instance
                |
                BindingFlags.Public
                |
                BindingFlags.NonPublic
            )
        )
        {
            if (
                !property.CanRead
                ||
                property
                    .GetIndexParameters()
                    .Length != 0
            )
            {
                continue;
            }


            if (
                !typeof(Template)
                .IsAssignableFrom(
                    property.PropertyType
                )
            )
            {
                continue;
            }


            try
            {
                Template value =
                    property.GetValue(
                        item,
                        null
                    )
                    as Template;


                if (
                    ReferenceEquals(
                        value,
                        wanted
                    )
                )
                {
                    return true;
                }
            }
            catch
            {
            }
        }


        return false;
    }
}