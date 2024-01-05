using SharpSheets.Cards.CardConfigs;
using SharpSheets.Cards.Definitions;
using SharpSheets.Exceptions;
using SharpSheets.Parsing;
using SharpSheets.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpSheets.Cards.CardSubjects {
	public class UnknownFeatureConfigException : SharpParsingException {

		private readonly DynamicCardSectionConfig sectionConfig;

		public override string Message => GetMessage();

		public UnknownFeatureConfigException(DocumentSpan? location, DynamicCardSectionConfig sectionConfig)
			: base(location, "No matching feature configuration.") {
			this.sectionConfig = sectionConfig;
		}

		public override object Clone() {
			return new UnknownFeatureConfigException(Location, sectionConfig);
		}

		private string GetMessage() {
			if(sectionConfig.cardFeatures.Count == 0) {
				return "This section does not provide any feature configurations.";
			}
			/*
			else if(sectionConfig.cardFeatures.Count == 1) {
				CardFeatureConfig featureConfig = sectionConfig.cardFeatures.First().Value;

				ConstantDefinition[] requiredDefinitions = featureConfig.definitions.OfType<ConstantDefinition>().ToArray();
				if (requiredDefinitions.Length > 0) {
					string names = GetRequiredDefinitionNames(requiredDefinitions);
					return "Feature must contain definitions for the following: " + names;
				}
				else {
					return "Feature does not match available configuration, please check documentation.";
				}
			}
			*/
			else {
				StringBuilder message = new StringBuilder();
				message.Append("No matching configuration found for feature. Must match " + (sectionConfig.cardFeatures.Count > 1 ? "one of " : "") + "the following:");

				foreach(Conditional<CardFeatureConfig> feature in sectionConfig.cardFeatures) {
					message.Append("\n- ");

					List<string> parts = new List<string>();

					ConstantDefinition[] requiredDefinitions = feature.Value.definitions.OfType<ConstantDefinition>().ToArray();
					if (requiredDefinitions.Length > 0) {
						parts.Add("defines: " + GetRequiredDefinitionNames(requiredDefinitions));
					}
					if (!feature.Condition.IsConstant) {
						parts.Add("satisfies: " + feature.Condition.ToString());
					}

					string featureReq = string.Join("; and ", parts);
					message.Append(char.ToUpper(featureReq[0]) + featureReq.Substring(1));
				}

				return message.ToString();
			}
		}

		private static string GetRequiredDefinitionNames(ConstantDefinition[] requiredDefinitions) {
			return string.Join(", ", requiredDefinitions.Select(d => "$"+d.name.ToString()));
		}

	}

}