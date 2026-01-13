using System;
using System.Numerics;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.LYT
{

    /// <summary>
    /// Represents a swoop track obstacle position in a LYT file.
    /// </summary>
    /// <remarks>
    /// WHAT IS A LYTObstacle?
    ///
    /// A LYTObstacle represents a swoop track obstacle object used in racing mini-games. Obstacles
    /// block the player's path and must be avoided.
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// A LYTObstacle stores:
    /// 1. Model: The name of the MDL file (3D model) that represents the obstacle's visual appearance
    ///    - This is usually a barrier, wall, or other blocking object
    ///
    /// 2. Position: The X, Y, Z coordinates where the obstacle should be placed
    ///    - The obstacle is placed on the racing track at this position
    ///
    /// HOW IT WORKS:
    ///
    /// During a swoop race:
    /// 1. The game engine loads obstacle models at the positions specified in the LYT file
    /// 2. The obstacles are placed on the track to make the race more challenging
    /// 3. The player must steer around them to avoid crashing
    /// 4. If the player hits an obstacle, they crash and lose the race
    ///
    /// These are only used in areas that have swoop racing tracks. Most areas have no obstacles.
    ///
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py
    /// </remarks>
    [PublicAPI]
    public sealed class LYTObstacle
    {
        /// <summary>
        /// The name of the MDL file (3D model) that represents the obstacle's visual appearance.
        /// </summary>
        public ResRef Model { get; set; }

        /// <summary>
        /// The X, Y, Z coordinates where the obstacle should be placed on the racing track.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LYTObstacle()
        {
        }

        /// <summary>
        /// Constructor with model and position.
        /// </summary>
        /// <param name="model">The model name (ResRef).</param>
        /// <param name="position">The position in 3D space.</param>
        public LYTObstacle(ResRef model, Vector3 position)
        {
            Model = model;
            Position = position;
        }
    }
}

