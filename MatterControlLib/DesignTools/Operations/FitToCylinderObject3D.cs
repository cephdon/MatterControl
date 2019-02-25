﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using MatterHackers.DataConverters2D;
using MatterHackers.PolygonMesh.Rendering;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.Transform;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class FitToCylinderObject3D : TransformWrapperObject3D, IEditorDraw
	{
		private Vector3 boundsSize;

		private AxisAlignedBoundingBox cacheAabb;

		private Vector3 cacheBounds;

		private Matrix4X4 cacheRequestedMatrix = new Matrix4X4();
		private Matrix4X4 cacheThisMatrix;

		public FitToCylinderObject3D()
		{
			Name = "Fit to Cylinder".Localize();
		}

		[Description("Normally the part is expanded to the cylinders. This will try to center the weight of the part in the cylinder.")]
		public bool AlternateCentering { get; set; } = false;

		public double Diameter { get; set; }

		[DisplayName("Height")]
		public double SizeZ { get; set; }

		[Description("Allows you turn on and off applying the fit to the z axis.")]
		public bool StretchZ { get; set; } = true;

		private IObject3D FitBounds => Children.Last();

		public static async Task<FitToCylinderObject3D> Create(IObject3D itemToFit)
		{
			var fitToBounds = new FitToCylinderObject3D();
			using (fitToBounds.RebuildLock())
			{
				using (new CenterAndHeightMantainer(itemToFit))
				{
					var aabb = itemToFit.GetAxisAlignedBoundingBox();
					var bounds = new Object3D()
					{
						Visible = false,
						Color = new Color(Color.Red, 100),
						Mesh = PlatonicSolids.CreateCube()
					};

					// add all the children
					var scaleItem = new Object3D();
					fitToBounds.Children.Add(scaleItem);
					scaleItem.Children.Add(itemToFit);
					fitToBounds.Children.Add(bounds);

					fitToBounds.Diameter = Math.Sqrt(aabb.XSize * aabb.XSize + aabb.YSize * aabb.YSize);
					fitToBounds.boundsSize.Z = aabb.ZSize;

					fitToBounds.SizeZ = aabb.ZSize;

					await fitToBounds.Rebuild();
				}
			}

			return fitToBounds;
		}

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			if (layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				var aabb = UntransformedChildren.GetAxisAlignedBoundingBox();
				layer.World.RenderCylinderOutline(this.WorldMatrix(), aabb.Center, Diameter, aabb.ZSize, 30, Color.Red, 1, 1);
			}
		}

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			if (Children.Count == 2)
			{
				if (cacheRequestedMatrix != matrix
					|| cacheThisMatrix != Matrix
					|| cacheBounds != boundsSize)
				{
					using (FitBounds.RebuildLock())
					{
						FitBounds.Visible = true;
						cacheAabb = base.GetAxisAlignedBoundingBox(matrix);
						FitBounds.Visible = false;
					}
					cacheRequestedMatrix = matrix;
					cacheThisMatrix = Matrix;
					cacheBounds = boundsSize;
				}

				return cacheAabb;
			}

			return base.GetAxisAlignedBoundingBox(matrix);
		}

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Mesh)
				|| invalidateType.InvalidateType.HasFlag(InvalidateType.Children))
			{
				cacheThisMatrix = Matrix4X4.Identity;
				base.OnInvalidate(invalidateType);
			}

			base.OnInvalidate(invalidateType);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			using (RebuildLock())
			{
				using (new CenterAndHeightMantainer(this))
				{
					AdjustChildSize(null, null);

					UpdateBoundsItem();

					cacheRequestedMatrix = new Matrix4X4();
					var after = this.GetAxisAlignedBoundingBox();
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Matrix));
			return Task.CompletedTask;
		}

		private Matrix4X4 GetCenteringTransformExpandedToRadius(IEnumerable<IObject3D> items, double radius)
		{
			IEnumerable<Vector2> GetTranslatedXY()
			{
				foreach (var item in items)
				{
					foreach (var mesh in item.VisibleMeshes())
					{
						var worldMatrix = mesh.WorldMatrix(this);
						foreach (var vertex in mesh.Mesh.Vertices)
						{
							yield return new Vector2(vertex.Transform(worldMatrix));
						}
					}
				}
			}

			var circle = SmallestEnclosingCircle.MakeCircle(GetTranslatedXY());

			// move the circle center to the origin
			var centering = Matrix4X4.CreateTranslation(-circle.Center.X, -circle.Center.Y, 0);
			// scale to the fit size in x y
			double scale = radius / circle.Radius;
			var scalling = Matrix4X4.CreateScale(scale, scale, 1);

			return centering * scalling;
		}

		private Matrix4X4 GetCenteringTransformVisualCenter(IEnumerable<IObject3D> items, double goalRadius)
		{
			IEnumerable<(Vector2, Vector2 , Vector2)> GetPolygons()
			{
				foreach (var item in items)
				{
					foreach (var meshItem in item.VisibleMeshes())
					{
						var worldMatrix = meshItem.WorldMatrix(this);
						var faces = meshItem.Mesh.Faces;
						var vertices = meshItem.Mesh.Vertices;
						foreach (var face in faces)
						{
							if (face.normal.TransformNormal(worldMatrix).Z > 0)
							{
								yield return (
									new Vector2(vertices[face.v0].Transform(worldMatrix)),
									new Vector2(vertices[face.v1].Transform(worldMatrix)),
									new Vector2(vertices[face.v2].Transform(worldMatrix))
									);
							}
						}
					}
				}
			}

			var outsidePolygons = new List<List<IntPoint>>();

			var projection = new Polygons();

			// remove all holes from the polygons so we only center the major outlines
			var polygons = OrthographicZProjection.GetClipperPolygons(GetPolygons());
			foreach (var polygon in polygons)
			{
				if (polygon.GetWindingDirection() == 1)
				{
					outsidePolygons.Add(polygon);
				}
			}

			IVertexSource outsideSource = outsidePolygons.CreateVertexStorage();

			Vector2 center = outsideSource.GetWeightedCenter();

			outsideSource = new VertexSourceApplyTransform(outsideSource, Affine.NewTranslation(-center));

			double radius = MaxXyDistFromCenter(outsideSource);

			double scale = goalRadius / radius;
			var scalling = Matrix4X4.CreateScale(scale, scale, 1);

			var centering = Matrix4X4.CreateTranslation(-center.X, -center.Y, 0);

			return centering * scalling;
		}

		private static double MaxXyDistFromCenter(IVertexSource vertexSource)
		{
			double maxDistSqrd = 0.000001;
			var center = vertexSource.GetBounds().Center;
			foreach (var vertex in vertexSource.Vertices())
			{
				var position = vertex.position;
				var distSqrd = (new Vector2(position.X, position.Y) - new Vector2(center.X, center.Y)).LengthSquared;
				if (distSqrd > maxDistSqrd)
				{
					maxDistSqrd = distSqrd;
				}
			}

			return Math.Sqrt(maxDistSqrd);
		}

		private static double MaxXyDistFromCenter(Mesh mesh)
		{
			double maxDistSqrd = 0.000001;
			var center = mesh.GetAxisAlignedBoundingBox().Center;
			foreach (var vertex in mesh.Vertices)
			{
				var position = vertex;
				var distSqrd = (new Vector2(position.X, position.Y) - new Vector2(center.X, center.Y)).LengthSquared;
				if (distSqrd > maxDistSqrd)
				{
					maxDistSqrd = distSqrd;
				}
			}

			return Math.Sqrt(maxDistSqrd);
		}

		private void AdjustChildSize(object sender, EventArgs e)
		{
			if (Children.Count > 0)
			{
				var aabb = UntransformedChildren.GetAxisAlignedBoundingBox();
				ItemWithTransform.Matrix = Matrix4X4.Identity;
				var scale = Vector3.One;
				if (StretchZ)
				{
					scale.Z = SizeZ / aabb.ZSize;
				}

				var minXy = Math.Min(scale.X, scale.Y);
				scale.X = minXy;
				scale.Y = minXy;

				ItemWithTransform.Matrix = Object3DExtensions.ApplyAtPosition(ItemWithTransform.Matrix, aabb.Center, Matrix4X4.CreateScale(scale));
			}
		}

		private void UpdateBoundsItem()
		{
			if (Children.Count == 2)
			{
				var transformAabb = ItemWithTransform.GetAxisAlignedBoundingBox();
				var fitAabb = FitBounds.GetAxisAlignedBoundingBox();
				var fitSize = fitAabb.Size;
				if (boundsSize.X != 0 && boundsSize.Y != 0 && boundsSize.Z != 0
					&& (fitSize != boundsSize
					|| fitAabb.Center != transformAabb.Center))
				{
					FitBounds.Matrix *= Matrix4X4.CreateScale(
						boundsSize.X / fitSize.X,
						boundsSize.Y / fitSize.Y,
						boundsSize.Z / fitSize.Z);
					FitBounds.Matrix *= Matrix4X4.CreateTranslation(
						transformAabb.Center - FitBounds.GetAxisAlignedBoundingBox().Center);
				}

				if (AlternateCentering)
				{
					var test = GetCenteringTransformVisualCenter(UntransformedChildren, Diameter/2);
				}
				else
				{
					var test = GetCenteringTransformExpandedToRadius(UntransformedChildren, Diameter / 2);
				}
			}
		}
	}
}