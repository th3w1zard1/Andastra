using System;
using System.Numerics;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.LYT
{

    /// <summary>
    /// Represents a swoop track booster position in a LYT file.
    /// </summary>
    /// <remarks>
    /// WHAT IS A LYTTrack?
    ///
    /// A LYTTrack represents a swoop track booster object used in racing mini-games. When the player
    /// drives over a booster, they get a speed boost.
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// A LYTTrack stores:
    /// 1. Model: The name of the MDL file (3D model) that represents the booster's visual appearance
    ///    - This is usually a simple model like a glowing pad or ring
    ///
    /// 2. Position: The X, Y, Z coordinates where the booster should be placed
    ///    - The booster is placed on the racing track at this position
    ///
    /// HOW IT WORKS:
    ///
    /// During a swoop race:
    /// 1. The game engine loads booster models at the positions specified in the LYT file
    /// 2. When the player's swoop bike touches a booster, the game applies a speed boost
    /// 3. The boost makes the player go faster for a short time
    ///
    /// These are only used in areas that have swoop racing tracks. Most areas have no tracks.
    ///
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_data.py
    /// </remarks>
    [PublicAPI]
    public sealed class LYTTrack
    {
        /// <summary>
        /// The name of the MDL file (3D model) that represents the booster's visual appearance.
        /// </summary>
        public ResRef Model { get; set; }

        /// <summary>
        /// The X, Y, Z coordinates where the booster should be placed on the racing track.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LYTTrack()
        {
        }

        /// <summary>
        /// Constructor with model and position.
        /// </summary>
        /// <param name="model">The model name (ResRef).</param>
        /// <param name="position">The position in 3D space.</param>
        public LYTTrack(ResRef model, Vector3 position)
        {
            Model = model;
            Position = position;
        }
    }
}

