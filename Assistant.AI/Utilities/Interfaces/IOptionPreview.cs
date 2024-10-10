using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssistantAI.Utilities.Interfaces {
    /// <summary>
    /// Interface for options that have a preview.
    /// </summary>
    /// <typeparam name="T">The type of the option.</typeparam>
    public interface IOptionPreview<T> {
        /// <summary>
        /// Get the preview of the option.
        /// </summary>
        /// <param name="value">The value of the option.</param>
        /// <returns>The preview of the option.</returns>
        string GetPreview(T value);
        /// <summary>
        /// Parse the value of the option.
        /// </summary>
        /// <param name="value">The value to parse.</param>
        /// <returns>The parsed value and if it was successful.</returns>
        (T?, bool) Parse(string value);
    }
}
