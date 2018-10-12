﻿/*
Copyright (c) 2018, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/


using Markdig.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Library.Widgets.HardwarePage
{
	public class PrinterDetails : FlowLayoutWidget
	{
		private ThemeConfig theme;
		private PrinterInfo printerInfo;

		public PrinterDetails(PrinterInfo printerInfo, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.printerInfo = printerInfo;
			this.theme = theme;

			var headingRow = this.AddHeading(
				OemSettings.Instance.GetIcon(printerInfo.Make),
				printerInfo.Name);

			headingRow.AddChild(new HorizontalSpacer());
			headingRow.HAnchor = HAnchor.Stretch;

			var openButton = new TextButton("Open".Localize(), theme)
			{
				BackgroundColor = theme.AccentMimimalOverlay
			};
			openButton.Click += (s, e) =>
			{
				PrinterDetails.SwitchPrinters(printerInfo.ID);
			};
			headingRow.AddChild(openButton);


			this.AddChild(headingRow);
		}

		public override async void OnLoad(EventArgs args)
		{
			if (File.Exists(printerInfo.ProfilePath))
			{
				// load up the printer profile so we can get the MatterHackers Skew-ID out of it
				var profile = PrinterSettings.LoadFile(printerInfo.ProfilePath);

				// if there is a SID than get the data from that url and display it
				var sid = profile.GetValue(SettingsKey.matterhackers_sid);
				if (!string.IsNullOrWhiteSpace(sid))
				{
					var product = (await LoadProductData(sid)).ProductSku;
					// put in controls from the feed that show relevant printer information

					var row = new FlowLayoutWidget()
					{
						HAnchor = HAnchor.Stretch,
						Margin = new BorderDouble(top: theme.DefaultContainerPadding)
					};
					this.AddChild(row);

					var image = new ImageBuffer(150, 10);
					row.AddChild(new ImageWidget(image)
					{
						Margin = new BorderDouble(right: theme.DefaultContainerPadding),
						VAnchor = VAnchor.Top
					});

					ApplicationController.Instance.DownloadToImageAsync(image, product.FeaturedImage.ImageUrl, scaleToImageX: true);

					var descriptionBackground = new GuiWidget()
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit | VAnchor.Top,
						Padding = theme.DefaultContainerPadding
					};

					var description = new MarkdownWidget(theme)
					{
						MinimumSize = new VectorMath.Vector2(350, 0),
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit,
						Markdown = product.ProductDescription.Trim()
					};
					descriptionBackground.AddChild(description);

					descriptionBackground.BeforeDraw += (s, e) =>
					{
						var rect = new RoundedRect(descriptionBackground.LocalBounds, 3);
						e.Graphics2D.Render(rect, theme.SlightShade);
					};

					row.AddChild(descriptionBackground);

					var padding = theme.DefaultContainerPadding;

					var addonsColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
					{
						Padding = new BorderDouble(padding, padding, padding, 0),
						HAnchor = HAnchor.Stretch
					};

					var addonsSection = new SectionWidget("Upgrades and Accessories", addonsColumn, theme);
					this.AddChild(addonsSection);
					theme.ApplyBoxStyle(addonsSection);
					addonsSection.Margin = addonsSection.Margin.Clone(left: 0);

					foreach(var item in product.ProductListing.AddOns)
					{
						var icon = new ImageBuffer(80, 0);
						ApplicationController.Instance.DownloadToImageAsync(icon, item.FeaturedImage.ImageUrl, scaleToImageX: true);

						var link = new LinkLabel("More Info", theme, pointSize: theme.DefaultFontSize)
						{
							VAnchor = VAnchor.Center
						};
						link.Click += (s, e) =>
						{
							ApplicationController.Instance.LaunchBrowser($"https://www.matterhackers.com/store/l/{item.AddOnListingReference}/sk/{item.AddOnSkuReference}");
						};

						addonsColumn.AddChild(new AddOnRow(item.AddOnTitle, theme, link, icon)
						{
							HAnchor = HAnchor.Stretch,
						});
					}
				}
				else // show some test data
				{
					GuiWidget row;
					row = this.AddHeading("Parts & Accessories");
					row.Margin = row.Margin.Clone(top: 20);
					this.AddChild(row);

					if (printerInfo.Make == "BCN")
					{
						var accessoriesImage = new ImageBuffer();
						row = new ImageWidget(accessoriesImage);
						this.AddChild(row);

						ApplicationController.Instance.LoadRemoteImage(accessoriesImage, "https://i.imgur.com/io37z8h.png", false).ConfigureAwait(false);
					}

					row = this.AddHeading("Upgrades");
					row.Margin = row.Margin.Clone(top: 20);
					this.AddChild(row);

					var upgradesImage = new ImageBuffer();
					row = new ImageWidget(upgradesImage);
					this.AddChild(row);

					ApplicationController.Instance.LoadRemoteImage(upgradesImage, "https://i.imgur.com/kDiV2Da.png", false).ConfigureAwait(false);


					if (printerInfo.Make == "BCN")
					{
						var accessoriesImage = new ImageBuffer();
						row = new ImageWidget(accessoriesImage)
						{
							Margin = new BorderDouble(top: 30)
						};
						this.AddChild(row);

						ApplicationController.Instance.LoadRemoteImage(accessoriesImage, "https://i.imgur.com/rrEwKY9.png", false).ConfigureAwait(false);
					}
				}
			}

			base.OnLoad(args);
		}

		public async Task<ProductSidData> LoadProductData(string sid)
		{
			try
			{
				var client = new HttpClient();
				string json = await client.GetStringAsync($"https://mh-pls-prod.appspot.com/p/1/product-sid/{sid}?IncludeListingData=True");

				var result = JsonConvert.DeserializeObject<ProductSidData>(json);
				return result;
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error collecting or loading printer details: " + ex.Message);
			}

			return null;
		}

		public static void SwitchPrinters(string printerID)
		{
			var activePrinter = ApplicationController.Instance.ActivePrinter;

			if (printerID == "new"
				|| string.IsNullOrEmpty(printerID)
				|| printerID == activePrinter.Settings.ID)
			{
				// do nothing
			}
			else
			{
				// TODO: when this opens a new tab we will not need to check any printer
				if (activePrinter.Connection.PrinterIsPrinting
					|| activePrinter.Connection.PrinterIsPaused)
				{
					// TODO: Rather than block here, the UI elements driving the change should be disabled while printing/paused
					UiThread.RunOnIdle(() =>
						StyledMessageBox.ShowMessageBox("Please wait until the print has finished and try again.".Localize(), "Can't switch printers while printing".Localize())
					);
				}
				else
				{
					ProfileManager.Instance.LastProfileID = printerID;
					ProfileManager.Instance.LoadPrinter().ConfigureAwait(false);
				}
			}
		}

		private GuiWidget AddHeading(ImageBuffer icon, string text)
		{
			var row = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(top: 5)
			};

			row.AddChild(new ImageWidget(icon, false)
			{
				Margin = new BorderDouble(right: 4),
				VAnchor = VAnchor.Center
			});

			row.AddChild(
				new TextWidget(
					text,
					textColor: theme.Colors.PrimaryTextColor,
					pointSize: theme.DefaultFontSize)
					{
						VAnchor = VAnchor.Center
					});

			return row;
		}

		private GuiWidget AddHeading(string text)
		{
			var row = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(top: 5)
			};

			row.AddChild(
				new TextWidget(
					text,
					textColor: theme.Colors.PrimaryTextColor,
					pointSize: theme.DefaultFontSize));

			return row;
		}

		private class AddOnRow : SettingsItem
		{
			public AddOnRow(string text, ThemeConfig theme, GuiWidget optionalControls, ImageBuffer icon)
				: base(text, theme, null, optionalControls, icon)
			{
				imageWidget.Cursor = Cursors.Hand;
				imageWidget.Margin = 4;
				imageWidget.Click += (s, e) =>
				{
					optionalControls.InvokeClick();
				};
			}
		}
	}
}
