using System;
using System.Collections.Generic;

namespace BioWare.NET.Resource.Formats.LIP
{
    /// <summary>
    /// Defines transition rules for interpolating between discrete viseme shapes.
    /// Implements a transition matrix that maps from/to shape pairs to intermediate
    /// shapes for smooth mouth animation interpolation.
    /// </summary>
    /// <remarks>
    /// Based on phoneme characteristics and natural mouth movement patterns:
    /// - Vowels (AH, EE, EH, OH, OOH) transition smoothly through intermediate vowels
    /// - Consonants with similar mouth positions transition directly
    /// - Dissimilar shapes use neutral or closest intermediate shapes
    /// - Transition matrix ensures natural-looking lip sync animation
    /// </remarks>
    public static class LIPShapeTransitionMatrix
    {
        // Transition matrix: [fromShape][toShape] = array of intermediate shapes with weights
        // Each entry is a list of (shape, weight) pairs where weights sum to 1.0
        // When interpolating from shape A to shape B with factor t:
        // - t=0.0: use fromShape (shape A)
        // - t=1.0: use toShape (shape B)
        // - t=0.0-1.0: blend intermediate shapes based on transition matrix
        private static readonly Dictionary<Tuple<LIPShape, LIPShape>, TransitionEntry> _transitionMatrix;

        /// <summary>
        /// Represents a transition entry with intermediate shapes and their blend weights.
        /// </summary>
        private class TransitionEntry
        {
            public List<Tuple<LIPShape, float>> IntermediateShapes { get; }

            public TransitionEntry()
            {
                IntermediateShapes = new List<Tuple<LIPShape, float>>();
            }

            public TransitionEntry(LIPShape shape, float weight)
            {
                IntermediateShapes = new List<Tuple<LIPShape, float>> { Tuple.Create(shape, weight) };
            }

            public TransitionEntry(List<Tuple<LIPShape, float>> shapes)
            {
                IntermediateShapes = shapes ?? new List<Tuple<LIPShape, float>>();
            }
        }

        static LIPShapeTransitionMatrix()
        {
            _transitionMatrix = new Dictionary<Tuple<LIPShape, LIPShape>, TransitionEntry>();
            InitializeTransitionMatrix();
        }

        /// <summary>
        /// Gets the interpolated shape when transitioning from one shape to another.
        /// </summary>
        /// <param name="fromShape">The source viseme shape.</param>
        /// <param name="toShape">The target viseme shape.</param>
        /// <param name="interpolationFactor">Interpolation factor (0.0 = fromShape, 1.0 = toShape).</param>
        /// <returns>The interpolated shape based on the transition matrix.</returns>
        public static LIPShape GetInterpolatedShape(LIPShape fromShape, LIPShape toShape, float interpolationFactor)
        {
            // Clamp interpolation factor
            interpolationFactor = Math.Max(0.0f, Math.Min(1.0f, interpolationFactor));

            // Same shape - no transition needed
            if (fromShape == toShape)
            {
                return fromShape;
            }

            // At endpoints, return the appropriate shape
            if (interpolationFactor <= 0.0f)
            {
                return fromShape;
            }
            if (interpolationFactor >= 1.0f)
            {
                return toShape;
            }

            // Look up transition entry
            var key = Tuple.Create(fromShape, toShape);
            if (!_transitionMatrix.TryGetValue(key, out TransitionEntry entry))
            {
                // No specific transition defined - use direct interpolation with threshold
                return interpolationFactor > 0.5f ? toShape : fromShape;
            }

            // Use transition matrix to determine intermediate shape
            return GetShapeFromTransition(entry, fromShape, toShape, interpolationFactor);
        }

        /// <summary>
        /// Gets the shape from a transition entry based on interpolation factor.
        /// Uses a three-phase interpolation: fromShape -> intermediate -> toShape.
        /// </summary>
        private static LIPShape GetShapeFromTransition(TransitionEntry entry, LIPShape fromShape, LIPShape toShape, float factor)
        {
            // If no intermediate shapes defined, use simple threshold
            if (entry.IntermediateShapes.Count == 0)
            {
                return factor > 0.5f ? toShape : fromShape;
            }

            // Three-phase interpolation:
            // Phase 1 (factor 0.0-0.4): Blend from fromShape to intermediate
            // Phase 2 (factor 0.4-0.6): Use intermediate shape
            // Phase 3 (factor 0.6-1.0): Blend from intermediate to toShape
            if (factor < 0.4f)
            {
                // Early phase: prefer fromShape, but can blend to intermediate
                float phaseFactor = factor / 0.4f; // 0.0 to 1.0 in this phase
                // Use intermediate if factor is high enough in this phase
                if (phaseFactor > 0.5f && entry.IntermediateShapes.Count > 0)
                {
                    return entry.IntermediateShapes[0].Item1;
                }
                return fromShape;
            }
            else if (factor > 0.6f)
            {
                // Late phase: blend from intermediate to toShape
                float phaseFactor = (factor - 0.6f) / 0.4f; // 0.0 to 1.0 in this phase
                // Use intermediate if factor is low enough in this phase
                if (phaseFactor < 0.5f && entry.IntermediateShapes.Count > 0)
                {
                    return entry.IntermediateShapes[0].Item1;
                }
                return toShape;
            }
            else
            {
                // Middle phase: use intermediate shape
                // Select the intermediate shape with highest weight
                if (entry.IntermediateShapes.Count > 0)
                {
                    float maxWeight = 0.0f;
                    LIPShape bestShape = entry.IntermediateShapes[0].Item1;

                    foreach (var intermediate in entry.IntermediateShapes)
                    {
                        if (intermediate.Item2 > maxWeight)
                        {
                            maxWeight = intermediate.Item2;
                            bestShape = intermediate.Item1;
                        }
                    }
                    return bestShape;
                }
                // Fallback to threshold if no intermediates
                return factor > 0.5f ? toShape : fromShape;
            }
        }

        /// <summary>
        /// Initializes the transition matrix with phoneme-based transition rules.
        /// </summary>
        private static void InitializeTransitionMatrix()
        {
            // Helper function to add transition with single intermediate shape
            void AddTransition(LIPShape from, LIPShape to, LIPShape intermediate)
            {
                var key = Tuple.Create(from, to);
                _transitionMatrix[key] = new TransitionEntry(intermediate, 1.0f);
            }

            // Helper function to add transition with multiple intermediate shapes
            void AddTransitionMultiple(LIPShape from, LIPShape to, List<Tuple<LIPShape, float>> intermediates)
            {
                var key = Tuple.Create(from, to);
                _transitionMatrix[key] = new TransitionEntry(intermediates);
            }

            // VOWEL TRANSITIONS (smooth transitions through intermediate vowels)
            // EE (1) transitions
            AddTransition(LIPShape.EE, LIPShape.EH, LIPShape.EE); // EE -> EH: stay closer to EE early
            AddTransition(LIPShape.EE, LIPShape.AH, LIPShape.EH); // EE -> AH: through EH
            AddTransition(LIPShape.EE, LIPShape.OH, LIPShape.EH); // EE -> OH: through EH
            AddTransition(LIPShape.EE, LIPShape.OOH, LIPShape.EH); // EE -> OOH: through EH

            // EH (2) transitions
            AddTransition(LIPShape.EH, LIPShape.EE, LIPShape.EH);
            AddTransition(LIPShape.EH, LIPShape.AH, LIPShape.AH); // EH -> AH: smooth
            AddTransition(LIPShape.EH, LIPShape.OH, LIPShape.AH); // EH -> OH: through AH
            AddTransition(LIPShape.EH, LIPShape.OOH, LIPShape.OH); // EH -> OOH: through OH

            // AH (3) transitions (most open vowel - central position)
            AddTransition(LIPShape.AH, LIPShape.EE, LIPShape.EH); // AH -> EE: through EH
            AddTransition(LIPShape.AH, LIPShape.EH, LIPShape.AH);
            AddTransition(LIPShape.AH, LIPShape.OH, LIPShape.OH); // AH -> OH: smooth transition
            AddTransition(LIPShape.AH, LIPShape.OOH, LIPShape.OH); // AH -> OOH: through OH
            AddTransition(LIPShape.AH, LIPShape.Y, LIPShape.EH); // AH -> Y: through EH

            // OH (4) transitions (rounded)
            AddTransition(LIPShape.OH, LIPShape.EE, LIPShape.EH); // OH -> EE: through EH
            AddTransition(LIPShape.OH, LIPShape.EH, LIPShape.AH);
            AddTransition(LIPShape.OH, LIPShape.AH, LIPShape.AH);
            AddTransition(LIPShape.OH, LIPShape.OOH, LIPShape.OOH); // OH -> OOH: smooth (both rounded)

            // OOH (5) transitions (most rounded/pursed)
            AddTransition(LIPShape.OOH, LIPShape.EE, LIPShape.OH); // OOH -> EE: through OH
            AddTransition(LIPShape.OOH, LIPShape.EH, LIPShape.OH);
            AddTransition(LIPShape.OOH, LIPShape.AH, LIPShape.OH);
            AddTransition(LIPShape.OOH, LIPShape.OH, LIPShape.OOH);

            // Y (6) transitions (slight smile)
            AddTransition(LIPShape.Y, LIPShape.EE, LIPShape.EE); // Y -> EE: similar smile
            AddTransition(LIPShape.Y, LIPShape.EH, LIPShape.EE);
            AddTransition(LIPShape.Y, LIPShape.AH, LIPShape.EH);
            AddTransition(LIPShape.Y, LIPShape.OH, LIPShape.EH);
            AddTransition(LIPShape.Y, LIPShape.OOH, LIPShape.EH);

            // CONSONANT TRANSITIONS (grouped by mouth position similarity)
            // STS (7) - Teeth touching
            AddTransition(LIPShape.STS, LIPShape.TD, LIPShape.TD); // STS -> TD: similar tongue/teeth
            AddTransition(LIPShape.STS, LIPShape.SH, LIPShape.SH); // STS -> SH: similar rounded
            AddTransition(LIPShape.STS, LIPShape.TH, LIPShape.TH); // STS -> TH: similar tongue

            // FV (8) - Lower lip touches teeth
            AddTransition(LIPShape.FV, LIPShape.MPB, LIPShape.MPB); // FV -> MPB: lip movement
            AddTransition(LIPShape.FV, LIPShape.Neutral, LIPShape.Neutral);

            // NG (9) - Back of tongue up
            AddTransition(LIPShape.NG, LIPShape.KG, LIPShape.KG); // NG -> KG: similar back tongue
            AddTransition(LIPShape.NG, LIPShape.AH, LIPShape.AH); // NG -> AH: through open mouth

            // TH (10) - Tongue between teeth
            AddTransition(LIPShape.TH, LIPShape.TD, LIPShape.TD); // TH -> TD: similar tongue position
            AddTransition(LIPShape.TH, LIPShape.STS, LIPShape.STS);
            AddTransition(LIPShape.TH, LIPShape.AH, LIPShape.AH); // TH -> AH: open mouth

            // MPB (11) - Lips closed
            AddTransition(LIPShape.MPB, LIPShape.FV, LIPShape.FV); // MPB -> FV: lip opening
            AddTransition(LIPShape.MPB, LIPShape.OOH, LIPShape.OOH); // MPB -> OOH: similar lip shape
            AddTransition(LIPShape.MPB, LIPShape.Neutral, LIPShape.Neutral);
            AddTransition(LIPShape.MPB, LIPShape.AH, LIPShape.AH); // MPB -> AH: open from closed

            // TD (12) - Tongue up
            AddTransition(LIPShape.TD, LIPShape.STS, LIPShape.STS); // TD -> STS: similar tongue/teeth
            AddTransition(LIPShape.TD, LIPShape.TH, LIPShape.TH);
            AddTransition(LIPShape.TD, LIPShape.L, LIPShape.L); // TD -> L: tongue movement
            AddTransition(LIPShape.TD, LIPShape.AH, LIPShape.AH);

            // SH (13) - Rounded relaxed
            AddTransition(LIPShape.SH, LIPShape.OH, LIPShape.OH); // SH -> OH: similar rounded
            AddTransition(LIPShape.SH, LIPShape.OOH, LIPShape.OH);
            AddTransition(LIPShape.SH, LIPShape.STS, LIPShape.STS);
            AddTransition(LIPShape.SH, LIPShape.AH, LIPShape.AH);

            // L (14) - Tongue forward
            AddTransition(LIPShape.L, LIPShape.TD, LIPShape.TD); // L -> TD: tongue movement
            AddTransition(LIPShape.L, LIPShape.AH, LIPShape.AH);
            AddTransition(LIPShape.L, LIPShape.EE, LIPShape.EH);

            // KG (15) - Back of tongue raised
            AddTransition(LIPShape.KG, LIPShape.NG, LIPShape.NG); // KG -> NG: similar back tongue
            AddTransition(LIPShape.KG, LIPShape.AH, LIPShape.AH); // KG -> AH: through open
            AddTransition(LIPShape.KG, LIPShape.OOH, LIPShape.AH);

            // NEUTRAL (0) transitions (closed/rest position)
            AddTransition(LIPShape.Neutral, LIPShape.MPB, LIPShape.MPB); // Neutral -> MPB: similar closed
            AddTransition(LIPShape.Neutral, LIPShape.OOH, LIPShape.OOH); // Neutral -> OOH: through rounded
            AddTransition(LIPShape.Neutral, LIPShape.AH, LIPShape.AH); // Neutral -> AH: open from rest
            AddTransition(LIPShape.Neutral, LIPShape.EE, LIPShape.EH);

            // VOWEL TO CONSONANT TRANSITIONS
            // Vowels to closed consonants (MPB, FV)
            AddTransition(LIPShape.AH, LIPShape.MPB, LIPShape.OOH); // AH -> MPB: through rounded
            AddTransition(LIPShape.EE, LIPShape.MPB, LIPShape.EH);
            AddTransition(LIPShape.OH, LIPShape.MPB, LIPShape.OOH);
            AddTransition(LIPShape.OOH, LIPShape.MPB, LIPShape.MPB);

            // Vowels to tongue consonants (TD, TH, L, NG, KG)
            AddTransition(LIPShape.AH, LIPShape.TD, LIPShape.AH);
            AddTransition(LIPShape.AH, LIPShape.TH, LIPShape.AH);
            AddTransition(LIPShape.AH, LIPShape.L, LIPShape.AH);
            AddTransition(LIPShape.AH, LIPShape.NG, LIPShape.AH);
            AddTransition(LIPShape.AH, LIPShape.KG, LIPShape.AH);

            // Vowels to rounded consonants (SH)
            AddTransition(LIPShape.AH, LIPShape.SH, LIPShape.OH);
            AddTransition(LIPShape.OH, LIPShape.SH, LIPShape.SH);
            AddTransition(LIPShape.OOH, LIPShape.SH, LIPShape.OH);

            // CONSONANT TO VOWEL TRANSITIONS
            // Closed consonants to vowels
            AddTransition(LIPShape.MPB, LIPShape.AH, LIPShape.AH);
            AddTransition(LIPShape.MPB, LIPShape.EE, LIPShape.EH);
            AddTransition(LIPShape.MPB, LIPShape.OH, LIPShape.OOH);
            AddTransition(LIPShape.FV, LIPShape.AH, LIPShape.AH);

            // Tongue consonants to vowels
            AddTransition(LIPShape.TD, LIPShape.AH, LIPShape.AH);
            AddTransition(LIPShape.TH, LIPShape.AH, LIPShape.AH);
            AddTransition(LIPShape.L, LIPShape.EE, LIPShape.EH);
            AddTransition(LIPShape.L, LIPShape.AH, LIPShape.AH);
            AddTransition(LIPShape.NG, LIPShape.AH, LIPShape.AH);
            AddTransition(LIPShape.KG, LIPShape.AH, LIPShape.AH);

            // Rounded consonants to vowels
            AddTransition(LIPShape.SH, LIPShape.OH, LIPShape.OH);
            AddTransition(LIPShape.SH, LIPShape.OOH, LIPShape.OH);
            AddTransition(LIPShape.SH, LIPShape.AH, LIPShape.OH);

            // Teeth consonants to vowels
            AddTransition(LIPShape.STS, LIPShape.EE, LIPShape.EH);
            AddTransition(LIPShape.STS, LIPShape.AH, LIPShape.AH);
        }
    }
}

