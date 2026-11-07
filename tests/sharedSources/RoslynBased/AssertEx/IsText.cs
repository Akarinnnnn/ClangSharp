// Copyright (c) .NET Foundation and Contributors. All Rights Reserved. Licensed under the MIT License (MIT).
// See License.md in the dotnet/roslyn repository root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using DiffPlex;
using DiffPlex.Chunkers;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using NUnit.Framework.Internal;

namespace Roslyn.Test.Utilities;

public static class IsText
{
    private static readonly IChunker s_lineChunker = new LineChunker();
    private static readonly IChunker s_lineEndingsPreservingChunker = new LineEndingsPreservingChunker();
    private static readonly InlineDiffBuilder s_diffBuilder = new InlineDiffBuilder(new Differ());

    private sealed class EqualTextOrDiffConstraint(string expected) : Constraint(expected)
    {
        public string Expected { get; } = expected;
        public override string Description => TestExecutionContext.CurrentContext.CurrentValueFormatter(Expected);

        private sealed class TextDiffConstraintResult(EqualTextOrDiffConstraint constraint, object? actualValue, bool isTypeMatch, string? diffMessageString)
            : ConstraintResult(constraint, actualValue, isTypeMatch && diffMessageString is null)
        {
            public bool IsTypeMatch { get; } = isTypeMatch;
            public string? DiffMessageString { get; } = diffMessageString;

            public override void WriteMessageTo(MessageWriter writer)
            {
                if (!IsTypeMatch)
                {
                    writer.WriteMessageLine("Actual is not a string: ");
                    writer.WriteActualValue(ActualValue);
                    writer.WriteLine();
                }
                else if (DiffMessageString is not null)
                {
                    writer.WriteLine(DiffMessageString);
                }
            }
        }

        public sealed override ConstraintResult ApplyTo<TActual>(TActual actualGeneric)
        {
            Debug.Assert(Expected is not null);
            bool isTypeMatch;
            string? diffMessageString;
            if (actualGeneric is null || typeof(TActual) != typeof(string) || actualGeneric is not string)
            {
                isTypeMatch = false;
                diffMessageString = null;
            }
            else if (typeof(TActual) == typeof(string))
            {
                isTypeMatch = true;
                var actual = actualGeneric as string;
                Debug.Assert(actual is not null);

                diffMessageString = GetDiffMessage(actual);
            }
            else
            {
                isTypeMatch = false;
                diffMessageString = null;
            }

            return new TextDiffConstraintResult(this, actualGeneric, isTypeMatch, diffMessageString);
        }

        private string? GetDiffMessage(string actual)
        {
            if (Expected == actual)
            {
                return null;
            }

            var diff = s_diffBuilder.BuildDiffModel(Expected, actual, ignoreWhitespace: false, ignoreCase: false, s_lineChunker);
            var messageBuilder = new StringBuilder();
            _ = messageBuilder.AppendLine("Actual and expected values differ. Expected shown in baseline of diff:");

            if (!diff.Lines.Any(line => line.Type is ChangeType.Inserted or ChangeType.Deleted))
            {
                // We have a failure only caused by line ending differences; recalculate with line endings visible
                diff = s_diffBuilder.BuildDiffModel(Expected, actual, ignoreWhitespace: false, ignoreCase: false, s_lineEndingsPreservingChunker);
            }

            foreach (var line in diff.Lines)
            {
                _ = line.Type switch {
                    ChangeType.Inserted => messageBuilder.Append('+'),
                    ChangeType.Deleted => messageBuilder.Append('-'),
                    _ => messageBuilder.Append(' '),
                };

                _ = messageBuilder.AppendLine(line.Text.Replace("\r", "<CR>", StringComparison.Ordinal).Replace("\n", "<LF>", StringComparison.Ordinal));
            }

            return messageBuilder.ToString();
        }
    }

    /// <summary>
    /// Asserts that two strings are equal, and prints a diff between the two if they are not.
    /// </summary>
    /// <param name="expected">The expected string. This is presented as the "baseline/before" side in the diff.</param>
    public static Constraint EqualToOrDiff(string expected) => new EqualTextOrDiffConstraint(expected);
}
