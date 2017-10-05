﻿using Shields.GraphViz.Components;
using Shields.GraphViz.Models;
using Shields.GraphViz.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Compilers
{
    /**
     * Utility class for create graph from a list of symbols
     * (Usually posfix)
     * */
    class GraphCreator
    {

        List<Symbol> symbols;
        SortedList<Int32, Image> images;
        List<GraphStruct> graphs;
        IRenderer renderer;

        internal List<Symbol> Symbols { get => symbols; set => symbols = value; }
        internal SortedList<Int32, Image> Images { get => images; set => images = value; }

        public GraphCreator()
        {
            Symbols = new List<Symbol>();
            Images = new SortedList<Int32, Image>();
            graphs = new List<GraphStruct>();
            renderer = new Renderer(@"C:\Program Files (x86)\Graphviz2.38\bin");
        }

        /**
         * Iterate over symbols and create image depending on symbol type(Operator, non-Operator, etc.)
         * */
        public async Task<bool> CreateImages()
        {
            //images.Clear();
            int i = 0;
            
            var ep = ImmutableDictionary.CreateBuilder<Id, Id>();
            ep.Add("label", "ε");

            foreach (Symbol s in Symbols)
            {
                List<Int32> nodes = new List<Int32>();
                List<Edge> edges = new List<Edge>();

                // Create 4 nodes when are symbol, + or *
                if (!s.IsOperator() || s.Coef.Equals("+") || s.Coef.Equals("*"))
                {
                    nodes = new List<Int32>() { 1, 2, 3, 4 };
                    edges = new List<Edge>() {new Edge(1, 2, "ε"), new Edge(3, 4, "ε")};
                }

                if (!s.IsOperator())
                {
                    Graph graph = CreateRegularGraph(s, s.Coef);

                    await CreateImageFromGraph(graph, i);
                    edges.Add(new Edge(2, 3, s.Coef));
                    edges.Add(new Edge(3, 4, "ε"));
                    graphs.Add(new GraphStruct(nodes, edges));
                } else if(s.Coef.Equals("+"))
                {
                    // Add positive kleene closure to previous symbol
                    Graph graph = CreateRegularGraph(Symbols[i - 1], s.Coef)
                                  .Add(new EdgeStatement("3", "2", ep.ToImmutable()));

                    await CreateImageFromGraph(graph, i);
                    edges.Add(new Edge(2, 3, Symbols[i - 1].Coef));
                    edges.Add(new Edge(3, 2, "ε"));
                    graphs.RemoveAt(graphs.Count - 1);
                    graphs.Add(new GraphStruct(nodes, edges));
                } else if (s.Coef.Equals("*"))
                {
                    // Add star kleene closure to previous symbol
                    Graph graph = CreateRegularGraph(Symbols[i - 1], s.Coef)
                                  .Add(new EdgeStatement("3", "2", ep.ToImmutable()))
                                  .Add(new EdgeStatement("1", "4", ep.ToImmutable()));
                    await CreateImageFromGraph(graph, i);

                    edges.Add(new Edge(2, 3, Symbols[i - 1].Coef));
                    edges.Add(new Edge(3, 2, "ε"));
                    edges.Add(new Edge(1, 4, "ε"));
                    graphs.RemoveAt(graphs.Count - 1);
                    graphs.Add(new GraphStruct(nodes, edges));
                } else if (s.Coef.Equals("."))
                {
                    ConcatOperation();
                    await CreateGraphFromStackTop("Concat operation");
                }
                i++;
            }

            return true;
        }

        private async Task CreateGraphFromStackTop(String title)
        {
            List<EdgeStatement> edges = new List<EdgeStatement>();

            foreach(Edge e in graphs.Last().Edges)
            {
                var label = ImmutableDictionary.CreateBuilder<Id, Id>();
                label.Add("label", e.Label);

                edges.Add(new EdgeStatement(e.Origin.ToString(), e.Dest.ToString(), label.ToImmutable()));
            }

            Graph graph = Graph.Directed
                .Add(AttributeStatement.Graph.Set("rankdir", "LR"))
                .Add(AttributeStatement.Graph.Set("labelloc", "t"))
                .Add(AttributeStatement.Node.Set("style", "filled"))
                .Add(AttributeStatement.Node.Set("fillcolor", "#FDE3A7"))
                .Add(AttributeStatement.Graph.Set("label",  title))
                .AddRange(edges);

            using (Stream file = new MemoryStream())
            {
                await renderer.RunAsync(
                    graph, file,
                    RendererLayouts.Dot,
                    RendererFormats.Png,
                    CancellationToken.None);

                Images.Add(images.Count, Image.FromStream(file));
            }
        }

        /**
         * Join (stack top - 1) with stack top.
         * 
         * First add edges to avoid update of graphs.Count
         * 
         * */
        private void ConcatOperation()
        {
            GraphStruct topGraph = graphs.Last();
            GraphStruct prevGraph = graphs.ElementAt(graphs.Count - 2);

            prevGraph.RemoveLastNode();

            int nodesCount = prevGraph.Nodes.Count;

            //topGraph.RemoveFirstNode();

            // Edge to connect last node of prevGraph with first node of stack top.
            prevGraph.Edges.Add(new Edge(nodesCount, nodesCount + 1, "ε"));

            // Add stack top edges to (top-1) edges
            foreach (Edge e in topGraph.Edges)
                prevGraph.Edges.Add(new Edge(e.Origin + nodesCount, e.Dest + nodesCount, e.Label));

            foreach(var node in topGraph.Nodes)
                prevGraph.Nodes.Add(node + nodesCount);

            // Delete stack top because it's already concatenate.
            graphs.RemoveAt(graphs.Count - 1);
        }

        /**
         * Create graph with 4 nodes, 2 epsilon edges and one coef
         *         
         *         e      S      e
         *      a ---> b ---> c ---> d
         **/
        private Graph CreateRegularGraph(Symbol s, String title)
        {
            var ep = ImmutableDictionary.CreateBuilder<Id, Id>();
            ep.Add("label", "ε");

            var label = ImmutableDictionary.CreateBuilder<Id, Id>();
            label.Add("label", s.Coef);

            return Graph.Directed
                .Add(AttributeStatement.Graph.Set("rankdir", "LR"))
                .Add(AttributeStatement.Graph.Set("labelloc", "t"))
                //.Add(AttributeStatement.Graph.Set("bgcolor", "#34495e"))
                .Add(AttributeStatement.Node.Set("style", "filled"))
                .Add(AttributeStatement.Node.Set("fillcolor", "#ECF0F1"))
                .Add(AttributeStatement.Graph.Set("label", "Graph for " + title))
                .Add(new EdgeStatement("1", "2", ep.ToImmutable()))
                .Add(new EdgeStatement("2", "3", label.ToImmutable()))
                .Add(new EdgeStatement("3", "4", ep.ToImmutable()));
        }

        /**
         * Create image asynchronously from graph and add it to list when ready
         * */
        private async Task CreateImageFromGraph(Graph graph, int i)
        {
            using (Stream file = new MemoryStream())
            {
                await renderer.RunAsync(
                    graph, file,
                    RendererLayouts.Dot,
                    RendererFormats.Png,
                    CancellationToken.None);

                Images.Add(i, Image.FromStream(file));
            }
        }
    }
}