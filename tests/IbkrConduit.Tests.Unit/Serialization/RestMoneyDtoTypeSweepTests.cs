using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using IbkrConduit.Portfolio;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Serialization;

/// <summary>
/// Pins the design-doc §6.5 / ADR-0001 money-numeric rule (D2): money and quantity fields on public
/// DTOs are <see cref="decimal"/> (<c>decimal?</c> when wire-optional), never <see cref="double"/>
/// or <see cref="float"/> — those binary floating types silently corrupt exact monetary values. This
/// reflection sweep walks every public type in the library assembly and fails if any
/// <see cref="JsonPropertyNameAttribute"/>-mapped member is typed <c>double</c>/<c>float</c> (nullable
/// or not). It is the PVR-02 guard: the pre-rule <c>double</c> surfaces on the account-summary and
/// event-contract DTOs must be retyped, and no future wire DTO may reintroduce a binary-float money field.
/// </summary>
public class RestMoneyDtoTypeSweepTests
{
    [Fact]
    public void PublicWireDtos_HaveNoDoubleOrFloatMappedMembers()
    {
        var offenders = FindBinaryFloatMappedMembers().ToList();

        offenders.ShouldBeEmpty(
            "Public wire DTOs must not map any field as double/float (§6.5 D2). Offenders: " +
            string.Join(", ", offenders));
    }

    private static IEnumerable<string> FindBinaryFloatMappedMembers()
    {
        var assembly = typeof(Position).Assembly;

        foreach (var type in assembly.GetTypes().Where(t => t is { IsPublic: true, IsClass: true }))
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetCustomAttribute<JsonPropertyNameAttribute>() is null)
                {
                    continue;
                }

                var effectiveType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (effectiveType == typeof(double) || effectiveType == typeof(float))
                {
                    yield return $"{type.Name}.{prop.Name} ({prop.PropertyType.Name})";
                }
            }
        }
    }
}
