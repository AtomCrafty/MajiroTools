using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Majiro {
	public static class Helpers {
		public static readonly Encoding ShiftJis;

		static Helpers() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			ShiftJis = Encoding.GetEncoding(932);
		}

		public static string ReadSizedString(this BinaryReader reader, int size, Encoding encoding = null) =>
			(encoding ?? ShiftJis).GetString(reader.ReadBytes(size));

		public static bool IsOneOf<T>(this T value, params T[] options) => options.Contains(value);

		private enum TopologicalSortState {
			Alive,
			Dead,
			Undead
		}

		public static void TopologicalSort<T>(this IList<T> nodes, Func<T, List<T>> dependencySelector) {
			var graph = nodes.ToDictionary(node => node, dependencySelector);
			var state = nodes.ToDictionary(node => node, _ => TopologicalSortState.Alive);
			Debug.Assert(graph.Count == nodes.Count);

			Console.WriteLine("digraph func {");
			foreach(var to in nodes) {
				foreach(var from in graph[to]) {
					Console.WriteLine($"{from} -> {to}");
				}
			}

			Console.WriteLine("}");

			nodes.Clear();
			foreach(var node in graph.Keys) Visit(node);

			void Visit(T node) {
				switch(state[node]) {
					case TopologicalSortState.Dead:
						return;
					case TopologicalSortState.Undead: // cycle
						Console.WriteLine("Cycle detected!");
						return;

					case TopologicalSortState.Alive:
						state[node] = TopologicalSortState.Undead;
						graph[node].ForEach(Visit);
						state[node] = TopologicalSortState.Dead;
						nodes.Add(node);
						break;
				}
			}
		}

		public static void PreOrderSort<T>(this IList<T> nodes, Func<T, List<T>> childSelector) {
			var graph = nodes.ToDictionary(node => node, childSelector);
			var added = new HashSet<T>();

			nodes.Clear();
			foreach(var node in graph.Keys) Visit(node);

			void Visit(T node) {
				if(!added.Add(node)) return;
				
				nodes.Add(node);
				graph[node].ForEach(Visit);
			}
		}
	}
}