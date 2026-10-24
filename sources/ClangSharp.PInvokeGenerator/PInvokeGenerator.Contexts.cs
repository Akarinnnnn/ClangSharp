// Copyright (c) .NET Foundation and Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ClangSharp;

public partial class PInvokeGenerator
{
    private ref struct ContextScope
    {
        private LinkedListNode<(Cursor, object?)> _node;
        private readonly PInvokeGenerator _parent;

        public ContextScope(Cursor cursor, object? context, PInvokeGenerator parent)
        {
            _parent = parent;

            var ctxLinkedList = _parent._context;

            _node = ctxLinkedList.AddLast((cursor, context));
        }

        public void Dispose()
        {
            if (_node is not null)
            {
                PopContext();

                _node = null!;
            }

        }


        [SuppressMessage("Style", "IDE0251:将成员设为“readonly”", Justification = "Mutate operation to _context")]
        private void PopContext()
        {
            var ctxLinkedList = _parent._context;

            var lastContextNode = ctxLinkedList.Last;
            var scopeMatch = lastContextNode == _node;
            Debug.Assert(scopeMatch, "Improperly paired Push & Pop calls to _context");

            if (scopeMatch)
            {
                ctxLinkedList.RemoveLast();
            }
        }
    }

    [SuppressMessage("Style", "IDE0250:将结构设置为 “readonly”", Justification = "This struct allows caller to modify context")]
    private ref struct Context<TCursor, TUserData> where TCursor : Cursor
        where TUserData : notnull
    {
        private readonly ref object? _user;

        private Context(TCursor c, ref object? user, LinkedListNode<(Cursor, object?)> selectedNode)
        {
            _user = ref user;
            Cursor = c;
            TargetContextNode = selectedNode;
        }

        public readonly LinkedListNode<(Cursor Cursor, object? UserData)> TargetContextNode { get; }

        public readonly TCursor Cursor { get; }


        public TUserData? UserData
        {
            readonly get
            {
                return (TUserData?)_user;
            }
            [SuppressMessage("Style", "IDE0251:将成员设为“readonly”", Justification = "Mutates context itself")]
            set
            {
                _user = value;
            }
        }

        /// <devdoc>
        /// <summary>
        /// Attempt to match a wrapper <see cref="Context{, }"/> starting from the previous context of <see cref="TargetContextNode"/>
        /// </summary>
        /// <typeparam name="TNewCursor"></typeparam>
        /// <typeparam name="TNewUserData"></typeparam>
        /// <param name="ctx">Valid if returned <see langword="true"/>.</param>
        /// <param name="selectContext">Pass <see langword="null"/> to match the previous of <see cref="TargetContextNode"/> only.</param>
        /// <returns>The <paramref name="ctx"> is valid when return <see langword="true"/>.</returns>
        /// </devdoc>
        internal readonly bool TryMatchPreviousContexts<TNewCursor, TNewUserData>(
            [MaybeNullWhen(false)] out Context<TNewCursor, TNewUserData> ctx,
            Func<LinkedListNode<(Cursor Cursor, object? UserData)>, LinkedListNode<(Cursor Cursor, object? UserData)>>? selectContext = null)
            where TNewCursor : Cursor where TNewUserData : notnull
        {
            var previousContext = TargetContextNode.Previous;
            Debug.Assert(previousContext is not null);

            return Context<TNewCursor, TNewUserData>.TryLoadFrom(previousContext, out ctx, selectContext);
        }

        internal readonly Context<TNewCursor, TUserData> CastCursor<TNewCursor>()
            where TNewCursor : TCursor
            => new((TNewCursor)Cursor, ref _user, TargetContextNode);

        internal static bool IsContextTypeMatch(Cursor c, object? user, [NotNullWhen(true)] out TCursor? goodCursor)
        {
            if (c is TCursor cursor)
            {
                goodCursor = cursor;
                return typeof(TUserData) == typeof(object) || user is TUserData;
            }
            else
            {
                goodCursor = null;
                return false;
            }
        }

        internal static bool TryLoadFrom(LinkedListNode<(Cursor Cursor, object? UserData)> contextNode,
            [MaybeNullWhen(false)] out Context<TCursor, TUserData> ctx,
            Func<LinkedListNode<(Cursor Cursor, object? UserData)>, LinkedListNode<(Cursor Cursor, object? UserData)>>? selectContext = null)
        {
            contextNode = selectContext?.Invoke(contextNode) ?? contextNode;

            var mappedCursor = contextNode.ValueRef.Cursor;
            ref var user = ref contextNode.ValueRef.UserData;

            if (IsContextTypeMatch(mappedCursor, user, out var goodCursor))
            {
                ctx = new(goodCursor, ref user, contextNode);
                return true;
            }
            else
            {
                ctx = default;
                return false;
            }
        }
    }
}
