﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TemplateEngine.cs" company="WildGums">
//   Copyright (c) 2008 - 2016 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SolutionGenerator.Templates
{
    using System;
    using System.Collections.Generic;
    using Catel;
    using Catel.Logging;
    using Catel.Reflection;

    public class TemplateEngine
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private const string TemplateKeyEnd = "]]";
        private const char ModifierSeparator = '|';

        private readonly TemplateLoader _templateLoader;

        private readonly Dictionary<string, Guid> _guids = new Dictionary<string, Guid>();

        public TemplateEngine(TemplateLoader templateLoader)
        {
            Argument.IsNotNull(() => templateLoader);

            _templateLoader = templateLoader;
        }

        public string ReplaceValues(string templateContent, ITemplate templateModel)
        {
            Argument.IsNotNullOrWhitespace(() => templateContent);
            Argument.IsNotNull(() => templateModel);

            Log.Debug("Filling template with template data");

            var templateModelType = templateModel.GetType();

            var templateContainer = templateModelType.Name;
            if (templateContainer.EndsWith("Template"))
            {
                templateContainer = templateContainer.Substring(0, templateContainer.Length - "Template".Length);
            }

            foreach (var templateProperty in templateModelType.GetPropertiesEx())
            {
                var templateKeyPrefix = $"[[{templateContainer.ToUpper()}.{templateProperty.Name.ToUpper()}";

                var templateValue = string.Empty;

                if (templateProperty.PropertyType.ImplementsInterfaceEx(typeof(INestedTemplate)))
                {
                    var nestedTemplateModel = PropertyHelper.GetPropertyValue<INestedTemplate>(templateModel, templateProperty.Name, false);
                    if (nestedTemplateModel != null)
                    {
                        templateValue = nestedTemplateModel.Callback(nestedTemplateModel.Tag);
                    }
                }
                else
                {
                    var templateObject = PropertyHelper.GetPropertyValue(templateModel, templateProperty.Name, false);
                    if (templateObject != null)
                    {
                        templateValue = ObjectToStringHelper.ToString(templateObject);
                    }
                }

                templateContent = ReplaceTemplateValue(templateContent, templateKeyPrefix, templateValue);
            }

            return templateContent;
        }

        protected virtual string ReplaceTemplateValue(string templateContent, string templateKeyPrefix, string templateValue)
        {
            var index = templateContent.IndexOfIgnoreCase(templateKeyPrefix);

            // TODO: Optimize by using regular expressions

            while (index >= 0)
            {
                Log.Debug($"Found template key '{templateKeyPrefix}' at position '{index}'");

                var endPos = templateContent.IndexOf(TemplateKeyEnd, index);
                if (endPos < 0)
                {
                    throw Log.ErrorAndCreateException<SolutionGeneratorException>($"Can't find end of key '{templateKeyPrefix}' at position '{index}'");
                }

                endPos += TemplateKeyEnd.Length;

                var length = endPos - index;

                var key = templateContent.Substring(index, length);
                templateContent = templateContent.Remove(index, length);

                var modifiersString = key.Remove(0, templateKeyPrefix.Length).Replace(TemplateKeyEnd, string.Empty);
                var modifiers = modifiersString.Split(new [] { ModifierSeparator }, StringSplitOptions.RemoveEmptyEntries);

                var value = templateValue;

                foreach (var modifier in modifiers)
                {
                    value = ApplyModifier(value, modifier);
                }

                templateContent = templateContent.Insert(index, value);

                Log.Debug($"Replaced template key '{templateKeyPrefix}' at position '{index}'");

                index = templateContent.IndexOfIgnoreCase(templateKeyPrefix);
            }

            return templateContent;
        }

        protected virtual string ApplyModifier(string value, string modifier)
        {
            modifier = modifier.Trim(ModifierSeparator);

            if (modifier.EqualsIgnoreCase(Modifiers.Lowercase))
            {
                value = value.ToLowerInvariant();
            }
            else if (modifier.EqualsIgnoreCase(Modifiers.Uppercase))
            {
                value = value.ToUpperInvariant();
            }
            else if (modifier.StartsWithIgnoreCase(Modifiers.Guid))
            {
                lock (_guids)
                {
                    Guid guid;

                    if (!_guids.TryGetValue(modifier, out guid))
                    {
                        guid = Guid.NewGuid();
                        _guids[modifier] = guid;
                    }

                    value = guid.ToString();
                }
            }
            else
            {
                throw Log.ErrorAndCreateException<SolutionGeneratorException>($"Modifier '{modifier}' is not supported");
            }

            return value;
        }
    }
}