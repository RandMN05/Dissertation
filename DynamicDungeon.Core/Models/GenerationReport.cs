using System.Collections.Generic;

namespace DynamicDungeon.Core.Models
{
    /// <summary>
    /// Diagnostic statistics produced by <see cref="DynamicDungeon.Core.MapGenerator.GenerateWithReport"/>.
    /// Engine-agnostic: contains only plain data, no Unity types.
    /// </summary>
    public class GenerationReport
    {
        /// <summary>Total wall-clock milliseconds from first attempt to success (or failure).</summary>
        public long TotalMs { get; set; }

        /// <summary>Number of attempts that were discarded due to validation failure.</summary>
        public int FailedAttempts { get; set; }

        /// <summary>The 1-based attempt index that succeeded (0 if all attempts failed).</summary>
        public int SuccessfulAttempt { get; set; }

        /// <summary>The seed used on the successful attempt.</summary>
        public int SuccessfulSeed { get; set; }

        /// <summary>Per-attempt failure reasons (index 0 = attempt 1).</summary>
        public List<string> AttemptFailureReasons { get; } = new List<string>();

        public bool Succeeded => SuccessfulAttempt > 0;
    }
}
