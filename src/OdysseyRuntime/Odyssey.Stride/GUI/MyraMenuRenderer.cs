using System;
using Stride.Rendering;
using Stride.Graphics;
using Stride.Engine;
using Stride.Rendering.Compositing;
using Stride.Games;
using Stride.Core;
using Stride.Core.Mathematics;
using RenderContext = Stride.Rendering.RenderContext;

using Myra;
using Myra.Graphics2D.UI;

namespace Odyssey.Stride.GUI
{
    /// <summary>
    /// Myra UI-based menu renderer with actual text buttons.
    /// Based on Myra UI in Stride Engine Tutorial: https://github.com/rds1983/Myra/wiki/Using-Myra-in-Stride-Engine-Tutorial
    /// </summary>
    public class MyraMenuRenderer : SceneRendererBase, IIdentifiable
    {
        private Desktop _desktop;
        private bool _isVisible = true;

        // Menu action callback
        public event EventHandler<int> MenuItemSelected;

        protected override void InitializeCore()
        {
            base.InitializeCore();

            // Initialize Myra environment
            // Based on Myra API: MyraEnvironment.Game must be set to the Stride Game instance
            // Source: https://github.com/rds1983/Myra/wiki/Using-Myra-in-Stride-Engine-Tutorial
            MyraEnvironment.Game = (Game)this.Services.GetService<IGame>();

            // Create main menu grid layout
            // Based on Myra API: Grid provides flexible layout system
            // Source: https://github.com/rds1983/Myra/wiki/Grid
            var grid = new Grid
            {
                RowSpacing = 20,
                ColumnSpacing = 20
            };

            // Set up grid columns - single centered column
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 400)); // Main menu width
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            // Set up grid rows - header + buttons + spacing
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 100)); // Header space
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 60)); // Start Game button
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 20)); // Spacing
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 60)); // Options button
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 20)); // Spacing
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 60)); // Exit button
            grid.RowsProportions.Add(new Proportion(ProportionType.Fill)); // Fill remaining space

            // Title label
            // Based on Myra API: Label displays text
            // Source: https://github.com/rds1983/Myra/wiki/Label
            var titleLabel = new Label
            {
                Id = "title",
                Text = "ODYSSEY ENGINE"
            };
            // Use attached properties via extension methods or direct property access
            titleLabel.GridColumn = 1;
            titleLabel.GridRow = 1;
            grid.Widgets.Add(titleLabel);

            // Start Game button
            // Based on Myra API: Button with Click event handler
            // Source: https://github.com/rds1983/Myra/wiki/Button
            var startButton = new Button
            {
                Content = new Label
                {
                    Text = "Start Game"
                }
            };
            startButton.GridColumn = 1;
            startButton.GridRow = 2;
            startButton.Click += (s, a) =>
            {
                Console.WriteLine("[MyraMenuRenderer] Start Game clicked");
                MenuItemSelected?.Invoke(this, 0);
            };
            grid.Widgets.Add(startButton);

            // Options button
            var optionsButton = new Button
            {
                Content = new Label
                {
                    Text = "Options"
                }
            };
            optionsButton.GridColumn = 1;
            optionsButton.GridRow = 4;
            optionsButton.Click += (s, a) =>
            {
                Console.WriteLine("[MyraMenuRenderer] Options clicked");
                MenuItemSelected?.Invoke(this, 1);
            };
            grid.Widgets.Add(optionsButton);

            // Exit button
            var exitButton = new Button
            {
                Content = new Label
                {
                    Text = "Exit"
                }
            };
            exitButton.GridColumn = 1;
            exitButton.GridRow = 6;
            exitButton.Click += (s, a) =>
            {
                Console.WriteLine("[MyraMenuRenderer] Exit clicked");
                MenuItemSelected?.Invoke(this, 2);
            };
            grid.Widgets.Add(exitButton);

            // Create desktop with grid as root
            // Based on Myra API: Desktop is the root container for all UI widgets
            // Source: https://github.com/rds1983/Myra/wiki/Desktop
            _desktop = new Desktop
            {
                Root = grid
            };

            Console.WriteLine("[MyraMenuRenderer] Myra UI menu initialized with text buttons");
        }

        protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
        {
            if (!_isVisible)
                return;

            // Clear depth buffer
            // Based on Stride API: CommandList.Clear clears render targets
            // DepthStencilClearOptions.DepthBuffer clears only the depth buffer
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/index.html
            drawContext.CommandList.Clear(GraphicsDevice.Presenter.DepthStencilBuffer, DepthStencilClearOptions.DepthBuffer);

            // Render Myra UI
            // Based on Myra API: Desktop.Render() renders all UI widgets
            // Source: https://github.com/rds1983/Myra/wiki/Desktop
            _desktop.Render();
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            Console.WriteLine($"[MyraMenuRenderer] Visibility set to: {visible}");
        }

        public bool IsVisible => _isVisible;
    }
}
