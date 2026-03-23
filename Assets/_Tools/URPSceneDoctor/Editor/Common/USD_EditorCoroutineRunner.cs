using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace URPSceneDoctor.Editor
{
    public static class USD_EditorCoroutineRunner
    {
        private static readonly List<CoroutineState> Active = new List<CoroutineState>();

        private sealed class CoroutineState
        {
            public readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
        }

        public static void StartCoroutineOwnerless(IEnumerator routine)
        {
            if (routine == null) return;

            var state = new CoroutineState();
            state.Stack.Push(routine);
            Active.Add(state);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var state = Active[i];
                if (!Step(state))
                {
                    Active.RemoveAt(i);
                }
            }

            if (Active.Count == 0)
            {
                EditorApplication.update -= Tick;
            }
        }

        private static bool Step(CoroutineState state)
        {
            while (state.Stack.Count > 0)
            {
                var top = state.Stack.Peek();
                if (!top.MoveNext())
                {
                    state.Stack.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    state.Stack.Push(nested);
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
