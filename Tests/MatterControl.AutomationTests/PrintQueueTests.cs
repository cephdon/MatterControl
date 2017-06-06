﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Category("MatterControl.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PrintQueueTests
	{
		[Test]
		public async Task AddOneItemToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 1
				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - one part  added and queue count increases by one
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec_Fox.stl", 2), "Named widget should exist after add(Fennec_Fox)");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task AddTwoItemsToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 2;
				int expectedCount = QueueData.Instance.ItemCount + 2;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);
				testRunner.Type(string.Format(
					"\"{0}\" \"{1}\"",
					MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"),
					MatterControlUtilities.GetTestItemPath("Batman.stl")));

				testRunner.Delay(2);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - two parts added and queue count increases by two
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 2 when adding 2 items");
				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec_Fox.stl", 2), "Named widget should exist after add(Fennec_Fox)");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman.stl", 2), "Named widget should exist after add(Batman)");

				return Task.CompletedTask;
			});
		}

		/// <summary>
		/// Tests that
		/// 1. Target item exists
		/// 2. QueueData.Instance.Count is correctly decremented after remove
		/// 3. Target item does not exist after remove
		/// </summary>
		[Test]
		public async Task RemoveButtonRemovesSingleItem()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.Delay(1);

				int expectedQueueCount = QueueData.Instance.ItemCount - 1;

				// Assert exists
				Assert.IsTrue(testRunner.NameExists("Queue Item 2013-01-25_Mouthpiece_v2"), "Target item should exist before Remove");

				// Remove target item
				testRunner.ClickByName("Queue Remove Button", 2);
				testRunner.Delay(1);

				// Assert removed
				Assert.AreEqual(expectedQueueCount, QueueData.Instance.ItemCount, "After Remove button click, Queue count should be 1 less");
				Assert.IsFalse(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 1), "Target item should not exist after Remove");

				return Task.CompletedTask;
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task RemoveButtonRemovesMultipleItems()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				/*
				 *Tests that when one item is selected  
				 *1. Queue Item count equals three before the test starts 
				 *2. Selecting multiple queue items and then clicking the Remove button removes the item 
				 *3. Selecting multiple queue items and then clicking the Remove button decreases the queue tab count by one
				 */

				int queueItemCount = QueueData.Instance.ItemCount;

				testRunner.Delay(2);

				testRunner.ClickByName("Queue Edit Button", 2);

				testRunner.ClickByName("Queue Item Batman", 2);

				testRunner.ClickByName("Queue Remove Button", 2);

				testRunner.Delay(1);

				int queueItemCountAfterRemove = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueItemCount - 2 == queueItemCountAfterRemove);

				bool queueItemExists = testRunner.WaitForName("Queue Item Batman", 2);
				bool secondQueueItemExists = testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 2);

				Assert.IsTrue(queueItemExists == false);
				Assert.IsTrue(secondQueueItemExists == false);

				return Task.CompletedTask;
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task QueueSelectCheckBoxWorks()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				/*
				 *Tests that when one item is selected  
				 *1. Queue Item count equals three before the test starts 
				 *2. Selecting multiple queue items and then clicking the Remove button removes the item 
				 *3. Selecting multiple queue items and then clicking the Remove button decreases the queue tab count by one
				 */

				int queueItemCount = QueueData.Instance.ItemCount;

				bool queueItemExists = testRunner.WaitForName("Queue Item Batman", 2);
				bool secondQueueItemExists = testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 2);

				SystemWindow systemWindow;
				GuiWidget rowItem = testRunner.GetWidgetByName("Queue Item Batman", out systemWindow, 3);

				SearchRegion rowItemRegion = testRunner.GetRegionByName("Queue Item Batman", 3);

				testRunner.ClickByName("Queue Edit Button", 3);

				GuiWidget foundWidget = testRunner.GetWidgetByName("Queue Item Checkbox", out systemWindow, 3, searchRegion: rowItemRegion);
				CheckBox checkBoxWidget = foundWidget as CheckBox;
				Assert.IsTrue(checkBoxWidget != null, "We should have an actual checkbox");
				Assert.IsTrue(checkBoxWidget.Checked == false, "currently not checked");

				testRunner.ClickByName("Queue Item Batman", 3);
				Assert.IsTrue(checkBoxWidget.Checked == true, "currently checked");

				testRunner.ClickByName("Queue Item Checkbox", 3, searchRegion: rowItemRegion);
				Assert.IsTrue(checkBoxWidget.Checked == false, "currently not checked");


				return Task.CompletedTask;
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task DragTo3DViewAddsItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddTestAssetsToLibrary("Batman.stl");

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;

				Assert.AreEqual(0, view3D.Scene.Children.Count, "The scene should have zero items before drag/drop");

				testRunner.DragDropByName("Row Item Batman", "centerPartPreviewAndControls");
				Assert.AreEqual(1, view3D.Scene.Children.Count, "The scene should have one item after drag/drop");

				testRunner.DragDropByName("Row Item Batman", "centerPartPreviewAndControls");
				Assert.AreEqual(2, view3D.Scene.Children.Count, "The scene should have two items after drag/drop");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task AddAmfFile()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 1
				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);

				testRunner.Type(MatterControlUtilities.GetTestItemPath("Rook.amf"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - one part  added and queue count increases by one
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item Rook.amf", 2), "Named widget should exist after add(Rook)");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task AddStlFile()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 1
				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);

				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - one part  added and queue count increases by one
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman.stl", 2), "Named widget should exist after add(Batman)");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task AddGCodeFile()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 1
				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);

				testRunner.Type(MatterControlUtilities.GetTestItemPath("chichen-itza_pyramid.gcode"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - one part  added and queue count increases by one
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item chichen-itza_pyramid.gcode", 2), "Named widget should exist after add(chichen-itza)");

				return Task.CompletedTask;
			});
		}
	}
}
