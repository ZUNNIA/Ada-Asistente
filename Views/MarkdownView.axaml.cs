using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using AsistenteVirtual.Converters;
using Markdig.Extensions.Tables;

namespace AsistenteVirtual.Views
{
    /// <summary>
    /// Control de usuario que renderiza contenido Markdown utilizando controles nativos de Avalonia.
    /// Optimizado para streaming de IA: utiliza un sistema de banderas para evitar saturación del hilo de UI.
    /// </summary>
    public partial class MarkdownView : UserControl
    {
        private static readonly MarkdownPipeline _pipeline;
        private bool _isRenderPending;
        private string? _lastRenderedMarkdown;

        /// <summary> Propiedad de dependencia para enlazar el texto Markdown. </summary>
        public static readonly StyledProperty<string> MarkdownProperty =
            AvaloniaProperty.Register<MarkdownView, string>(nameof(Markdown), string.Empty);

        /// <summary> Obtiene o establece el contenido Markdown a visualizar. </summary>
        public string Markdown
        {
            get => GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        static MarkdownView()
        {
            // Configuración del motor Markdig
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseEmojiAndSmiley()
                .Build();

            // Observador de cambios en la propiedad Markdown
            _ = MarkdownProperty.Changed.AddClassHandler<MarkdownView>((x, e) => x.RequestRender());
        }

        /// <summary>
        /// Inicializa una nueva instancia de <see cref="MarkdownView"/>.
        /// </summary>
        public MarkdownView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Solicita un ciclo de renderizado. Si ya hay uno pendiente en la cola del Dispatcher,
        /// ignora las peticiones subsiguientes para ahorrar recursos de CPU durante el streaming.
        /// </summary>
        private void RequestRender()
        {

            if (Markdown == _lastRenderedMarkdown) { return; }

            if (_isRenderPending) { return; }
            _isRenderPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    RenderMarkdownInternal();
                    _lastRenderedMarkdown = Markdown;
                }
                finally
                {
                    _isRenderPending = false;
                    if (Markdown != _lastRenderedMarkdown)
                    {
                        RequestRender();
                    }
                }
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Ejecuta la transformación física de Markdown a controles de Avalonia.
        /// </summary>
        private void RenderMarkdownInternal()
        {
            StackPanel? container = this.FindControl<StackPanel>("ContentPanel");
            if (container == null) { return; }

            string text = Markdown;
            if (string.IsNullOrWhiteSpace(text))
            {
                container.Children.Clear();
                return;
            }

            try
            {
                MarkdownDocument document = Markdig.Markdown.Parse(text, _pipeline);
                StackPanel tempPanel = new() { Spacing = 5 };
                MarkdownToAvaloniaRenderer renderer = new(tempPanel);
                renderer.RenderDocument(document);
                container.Children.Clear();
                List<Control> children = [.. tempPanel.Children];
                tempPanel.Children.Clear();
                container.Children.AddRange(children);
            }
            catch (Exception)
            {
                // Si un frame del streaming viene roto, no se actualiza hasta el siguiente
            }
        }


        /// <summary>
        /// Clase interna encargada de la visita al árbol de sintaxis de Markdown
        /// y la generación de los controles visuales correspondientes.
        /// </summary>
        /// <param name="initialParentPanel">Panel raíz donde se insertarán los elementos.</param>
        private sealed class MarkdownToAvaloniaRenderer(StackPanel initialParentPanel)
        {
            private readonly Stack<Panel> _blockStack = new([initialParentPanel]);
            private readonly Stack<InlineCollection> _inlineStack = new();

            /// <summary> Recorre el documento Markdown y genera la UI. </summary>
            /// <param name="document">Documento parseado por Markdig.</param>
            public void RenderDocument(MarkdownDocument document)
            {
                foreach (Block block in document)
                {
                    RenderBlock(block);
                }
            }

            private void RenderBlock(Block block)
            {
                switch (block)
                {
                    case ParagraphBlock p: RenderParagraph(p); break;
                    case HeadingBlock h: RenderHeading(h); break;
                    case FencedCodeBlock f: RenderFencedCode(f); break;
                    case ListBlock l: RenderList(l); break;
                    case QuoteBlock q: RenderQuote(q); break;
                    case ThematicBreakBlock: RenderHorizontalRule(); break;
                    case Table table: RenderTable(table); break;
                    default:
                        break;
                }
            }

            // --- PÁRRAFOS Y TEXTO ---
            private void RenderParagraph(ParagraphBlock p)
            {
                SelectableTextBlock tb = new() { TextWrapping = TextWrapping.Wrap, Classes = { "markdown-paragraph" } };
                _blockStack.Peek().Children.Add(tb);

                if (p.Inline != null)
                {
                    _inlineStack.Push(tb.Inlines!);
                    foreach (Markdig.Syntax.Inlines.Inline child in p.Inline)
                    {
                        RenderInline(child);
                    }

                    if (_inlineStack.Count > 0)
                    {
                        _ = _inlineStack.Pop();
                    }
                }
            }

            // --- ENCABEZADOS ---
            private void RenderHeading(HeadingBlock h)
            {
                SelectableTextBlock tb = new() { Classes = { $"markdown-h{h.Level}" } };
                _blockStack.Peek().Children.Add(tb);

                if (tb.Inlines != null && h.Inline != null)
                {
                    _inlineStack.Push(tb.Inlines);
                    foreach (Markdig.Syntax.Inlines.Inline child in h.Inline)
                    {
                        RenderInline(child);
                    }

                    _ = _inlineStack.Pop();
                }
            }

            // --- CITAS ---
            private void RenderQuote(QuoteBlock q)
            {
                Border quoteBorder = new() { Classes = { "markdown-quote" } };
                StackPanel quotePanel = new() { Spacing = 2 };
                quoteBorder.Child = quotePanel;

                _blockStack.Peek().Children.Add(quoteBorder);
                _blockStack.Push(quotePanel);
                foreach (Block innerBlock in q)
                {
                    RenderBlock(innerBlock);
                }

                _ = _blockStack.Pop();
            }

            // --- LÍNEA HORIZONTAL ---
            private void RenderHorizontalRule()
            {
                _blockStack.Peek().Children.Add(new Border { Classes = { "markdown-hr" } });
            }

            // --- TABLAS ---
            private void RenderTable(Table table)
            {
                Grid grid = new() { Classes = { "markdown-table" } };

                // Definir columnas
                if (table.Count > 0 && table[0] is TableRow firstRow)
                {
                    for (int i = 0; i < firstRow.Count; i++)
                    {
                        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    }
                }

                for (int rowIndex = 0; rowIndex < table.Count; rowIndex++)
                {
                    TableRow row = (TableRow)table[rowIndex];
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                    for (int colIndex = 0; colIndex < row.Count; colIndex++)
                    {
                        TableCell cell = (TableCell)row[colIndex];
                        Border cellBorder = new()
                        {
                            Classes = { row.IsHeader ? "markdown-table-header-clean" : "markdown-table-cell-clean" }
                        };

                        StackPanel cellContent = new();
                        cellBorder.Child = cellContent;

                        Grid.SetRow(cellBorder, rowIndex);
                        Grid.SetColumn(cellBorder, colIndex);
                        grid.Children.Add(cellBorder);

                        _blockStack.Push(cellContent);
                        foreach (Block b in cell)
                        {
                            RenderBlock(b);
                        }

                        _ = _blockStack.Pop();
                    }
                }
                _blockStack.Peek().Children.Add(grid);
            }

            // --- ELEMENTOS INLINE ---
            private void RenderInline(Markdig.Syntax.Inlines.Inline inline)
            {
                if (_inlineStack.Count == 0)
                {
                    return;
                }

                InlineCollection currentInlines = _inlineStack.Peek();

                switch (inline)
                {
                    case LiteralInline lit:
                        currentInlines.Add(new Run(lit.Content.ToString()));
                        break;
                    case CodeInline c:
                        currentInlines.Add(new Run(c.Content) { Classes = { "markdown-inline-code" } });
                        break;
                    case LineBreakInline:
                        currentInlines.Add(new LineBreak());
                        break;
                    case LinkInline link when link.IsImage:
                        RenderImage(link);
                        break;
                    case LinkInline link:
                        RenderLink(link);
                        break;
                    case EmphasisInline em:
                        Span span = new()
                        {
                            FontStyle = em.DelimiterCount == 1 ? FontStyle.Italic : FontStyle.Normal,
                            FontWeight = em.DelimiterCount >= 2 ? FontWeight.Bold : FontWeight.Normal
                        };
                        currentInlines.Add(span);
                        _inlineStack.Push(span.Inlines);
                        foreach (Markdig.Syntax.Inlines.Inline child in em)
                        {
                            RenderInline(child);
                        }

                        if (_inlineStack.Count > 0)
                        {
                            _ = _inlineStack.Pop();
                        }

                        break;
                    default:
                        break;
                }
            }

            // --- ENLACES ---
            private void RenderLink(LinkInline link)
            {
                HyperlinkButton hl = new()
                {
                    Content = link.FirstChild?.ToString() ?? link.Url,
                    NavigateUri = new Uri(link.Url ?? "about:blank"),
                    Classes = { "markdown-link" }
                };
                _inlineStack.Peek().Add(new InlineUIContainer(hl));
            }

            // --- IMÁGENES ---
            private void RenderImage(LinkInline link)
            {
                Image img = new() { Classes = { "markdown-image" }, MaxHeight = 350 };
                _blockStack.Peek().Children.Add(img);

                Dispatcher.UIThread.Post(async () =>
                {
                    img.Source = await UrlToBitmapConverter.GetOrFetchImageAsync(link.Url ?? "");
                });
            }
            // --- CÓDIGO ---
            private void RenderFencedCode(FencedCodeBlock f)
            {
                Border b = new()
                {
                    Classes = { "markdown-code-border" },
                    Child = new TextBox
                    {
                        Text = f.Lines.ToString(),
                        Classes = { "markdown-code-block" },
                        IsReadOnly = true
                    }
                };
                _blockStack.Peek().Children.Add(b);
            }
            // --- LISTAS ---
            private void RenderList(ListBlock l)
            {
                StackPanel listPanel = new() { Margin = new Thickness(15, 0, 0, 10) };
                _blockStack.Peek().Children.Add(listPanel);
                _blockStack.Push(listPanel);
                foreach (Block item in l)
                {
                    if (item is ListItemBlock li)
                    {
                        foreach (Block child in li)
                        {
                            RenderBlock(child);
                        }
                    }
                }
                _ = _blockStack.Pop();
            }
        }

        private void InitializeComponent()
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        }
    }
}