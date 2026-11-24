using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests where the debug sidebar goes - between the hotbar above it and the food below it
	/// </summary>
	public class VillageDebugPanelTests
	{
		[Fact]
		public void SidebarFitsBetweenTheFoodAndTheHotbarWithAGap()
		{
			Assert.Equal(new Vector2(-392f, 412f), VillageDebugPanel.Between(-540f, 540f, -400f, 420f));
		}

		[Fact]
		public void SidebarRunsToTheCanvasEdgeWhereItFindsNothing()
		{
			Assert.Equal(new Vector2(-392f, 540f), VillageDebugPanel.Between(-540f, 540f, -400f, null));
			Assert.Equal(new Vector2(-540f, 412f), VillageDebugPanel.Between(-540f, 540f, null, 420f));
			Assert.Equal(new Vector2(-540f, 540f), VillageDebugPanel.Between(-540f, 540f, null, null));
		}

		[Fact]
		public void SidebarStaysOnTheCanvas()
		{
			Assert.Equal(new Vector2(-540f, 540f), VillageDebugPanel.Between(-540f, 540f, -600f, 700f));
		}

		[Fact]
		public void SidebarCoversTheFullHeightWhenThereIsNoRoomBetween()
		{
			Assert.Equal(new Vector2(-540f, 540f), VillageDebugPanel.Between(-540f, 540f, 0f, 100f));
			Assert.Equal(new Vector2(-540f, 540f), VillageDebugPanel.Between(-540f, 540f, 300f, -300f));
		}
	}
}
