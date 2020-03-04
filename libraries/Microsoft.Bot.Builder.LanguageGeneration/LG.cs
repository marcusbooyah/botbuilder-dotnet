﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdaptiveExpressions;
using AdaptiveExpressions.Memory;

namespace Microsoft.Bot.Builder.LanguageGeneration
{
    /// <summary>
    /// LG entrance, including properties that LG file has, and evaluate functions.
    /// </summary>
    public class LG
    {
        public LG(
            IList<LGTemplate> templates = null,
            IList<LGImport> imports = null,
            IList<Diagnostic> diagnostics = null,
            IList<LG> references = null,
            string content = null,
            string id = null,
            ExpressionParser expressionParser = null,
            ImportResolverDelegate importResolver = null,
            IList<string> options = null)
        {
            Templates = templates ?? new List<LGTemplate>();
            Imports = imports ?? new List<LGImport>();
            Diagnostics = diagnostics ?? new List<Diagnostic>();
            References = references ?? new List<LG>();
            Content = content ?? string.Empty;
            ImportResolver = importResolver;
            Id = id ?? string.Empty;
            ExpressionParser = expressionParser ?? new ExpressionParser();
            Options = options ?? new List<string>();
        }

        /// <summary>
        /// Gets get all templates from current lg file and reference lg files.
        /// </summary>
        /// <value>
        /// All templates from current lg file and reference lg files.
        /// </value>
        public IList<LGTemplate> AllTemplates => new List<LG> { this }.Union(References).SelectMany(x => x.Templates).ToList();

        /// <summary>
        /// Gets get all diagnostics from current lg file and reference lg files.
        /// </summary>
        /// <value>
        /// All diagnostics from current lg file and reference lg files.
        /// </value>
        public IList<Diagnostic> AllDiagnostics => new List<LG> { this }.Union(References).SelectMany(x => x.Diagnostics).ToList();

        /// <summary>
        /// Gets or sets delegate for resolving resource id of imported lg file.
        /// </summary>
        /// <value>
        /// Delegate for resolving resource id of imported lg file.
        /// </value>
        public ImportResolverDelegate ImportResolver { get; set; }

        /// <summary>
        /// Gets or sets templates that this LG file contains directly.
        /// </summary>
        /// <value>
        /// templates that this LG file contains directly.
        /// </value>
        public IList<LGTemplate> Templates { get; set; }

        /// <summary>
        /// Gets or sets expression parser.
        /// </summary>
        /// <value>
        /// expression parser.
        /// </value>
        public ExpressionParser ExpressionParser { get; set; }

        /// <summary>
        /// Gets or sets import elements that this LG file contains directly.
        /// </summary>
        /// <value>
        /// import elements that this LG file contains directly.
        /// </value>
        public IList<LGImport> Imports { get; set; }

        /// <summary>
        /// Gets or sets all references that this LG file has from <see cref="Imports"/>.
        /// Notice: reference includes all child imports from the LG file,
        /// not only the children belong to this LG file directly.
        /// so, reference count may >= imports count. 
        /// </summary>
        /// <value>
        /// all references that this LG file has from <see cref="Imports"/>.
        /// </value>
        public IList<LG> References { get; set; }

        /// <summary>
        /// Gets or sets diagnostics.
        /// </summary>
        /// <value>
        /// diagnostics.
        /// </value>
        public IList<Diagnostic> Diagnostics { get; set; }

        /// <summary>
        /// Gets or sets LG content.
        /// </summary>
        /// <value>
        /// LG content.
        /// </value>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets id of this LG file.
        /// </summary>
        /// <value>
        /// id of this lg source. For file, is full path.
        /// </value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets lG file options.
        /// </summary>
        /// <value>
        /// LG file options.
        /// </value>
        public IList<string> Options { get; set; }

        /// <summary>
        /// Gets a value indicating whether lG parser/checker/evaluate strict mode.
        /// If strict mode is on, expression would throw exception instead of return
        /// null or make the condition failed.
        /// </summary>
        /// <value>
        /// A value indicating whether lG parser/checker/evaluate strict mode.
        /// If strict mode is on, expression would throw exception instead of return
        /// null or make the condition failed.
        /// </value>
        public bool StrictMode => GetStrictModeFromOptions(Options);

        /// <summary>
        /// Parser to turn lg content into a <see cref="LG"/>.
        /// </summary>
        /// <param name="filePath"> absolut path of a LG file.</param>
        /// <param name="importResolver">resolver to resolve LG import id to template text.</param>
        /// <param name="expressionParser">expressionEngine Expression engine for evaluating expressions.</param>
        /// <returns>new <see cref="LG"/> entity.</returns>
        public static LG ParseFile(
            string filePath,
            ImportResolverDelegate importResolver = null,
            ExpressionParser expressionParser = null) => LGParser.ParseFile(filePath, importResolver, expressionParser);

        /// <summary>
        /// Parser to turn lg content into a <see cref="LG"/>.
        /// </summary>
        /// <param name="content">Text content contains lg templates.</param>
        /// <param name="id">id is the identifier of content. If importResolver is null, id must be a full path string. </param>
        /// <param name="importResolver">resolver to resolve LG import id to template text.</param>
        /// <param name="expressionParser">expressionEngine parser engine for parsing expressions.</param>
        /// <returns>new <see cref="LG"/> entity.</returns>
        public static LG ParseText(
            string content,
            string id = "",
            ImportResolverDelegate importResolver = null,
            ExpressionParser expressionParser = null) => LGParser.ParseText(content, id, importResolver, expressionParser);

        /// <summary>
        /// Evaluate a template with given name and scope.
        /// </summary>
        /// <param name="templateName">Template name to be evaluated.</param>
        /// <param name="scope">The state visible in the evaluation.</param>
        /// <returns>Evaluate result.</returns>
        public object EvaluateTemplate(string templateName, object scope = null)
        {
            CheckErrors();

            var evaluator = new Evaluator(AllTemplates.ToList(), ExpressionParser, StrictMode);
            return evaluator.EvaluateTemplate(templateName, scope);
        }

        /// <summary>
        /// Use to evaluate an inline template str.
        /// </summary>
        /// <param name="inlineStr">inline string which will be evaluated.</param>
        /// <param name="scope">scope object or JToken.</param>
        /// <returns>Evaluate result.</returns>
        public object Evaluate(string inlineStr, object scope = null)
        {
            var (result, error) = ExpressionParser.Parse($"`{inlineStr}`").TryEvaluate(scope);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new Exception(error);
            }
            else
            {
                return result;
            }
        }

        /// <summary>
        /// Expand a template with given name and scope.
        /// Return all possible responses instead of random one.
        /// </summary>
        /// <param name="templateName">Template name to be evaluated.</param>
        /// <param name="scope">The state visible in the evaluation.</param>
        /// <returns>Expand result.</returns>
        public IList<string> ExpandTemplate(string templateName, object scope = null)
        {
            CheckErrors();
            var expander = new Expander(AllTemplates.ToList(), ExpressionParser, StrictMode);
            return expander.ExpandTemplate(templateName, scope);
        }

        /// <summary>
        /// (experimental)
        /// Analyzer a template to get the static analyzer results including variables and template references.
        /// </summary>
        /// <param name="templateName">Template name to be evaluated.</param>
        /// <returns>analyzer result.</returns>
        public AnalyzerResult AnalyzeTemplate(string templateName)
        {
            CheckErrors();
            var analyzer = new Analyzer(AllTemplates.ToList(), ExpressionParser);
            return analyzer.AnalyzeTemplate(templateName);
        }

        /// <summary>
        /// update an exist template.
        /// </summary>
        /// <param name="templateName">origin template name. the only id of a template.</param>
        /// <param name="newTemplateName">new template Name.</param>
        /// <param name="parameters">new params.</param>
        /// <param name="templateBody">new template body.</param>
        /// <returns>updated LG file.</returns>
        public LG UpdateTemplate(string templateName, string newTemplateName, List<string> parameters, string templateBody)
        {
            var template = Templates.FirstOrDefault(u => u.Name == templateName);
            if (template != null)
            {
                var templateNameLine = BuildTemplateNameLine(newTemplateName, parameters);
                var newTemplateBody = ConvertTemplateBody(templateBody);
                var content = $"{templateNameLine}\r\n{newTemplateBody}\r\n";
                var startLine = template.ParseTree.Start.Line - 1;
                var stopLine = template.ParseTree.Stop.Line - 1;

                var newContent = ReplaceRangeContent(Content, startLine, stopLine, content);
                Initialize(ParseText(newContent, Id, ImportResolver));
            }

            return this;
        }

        /// <summary>
        /// Add a new template and return LG File.
        /// </summary>
        /// <param name="templateName">new template name.</param>
        /// <param name="parameters">new params.</param>
        /// <param name="templateBody">new  template body.</param>
        /// <returns>updated LG file.</returns>
        public LG AddTemplate(string templateName, List<string> parameters, string templateBody)
        {
            var template = Templates.FirstOrDefault(u => u.Name == templateName);
            if (template != null)
            {
                throw new Exception(LGErrors.TemplateExist(templateName));
            }

            var templateNameLine = BuildTemplateNameLine(templateName, parameters);
            var newTemplateBody = ConvertTemplateBody(templateBody);
            var newContent = $"{Content.TrimEnd()}\r\n\r\n{templateNameLine}\r\n{newTemplateBody}\r\n";
            Initialize(ParseText(newContent, Id, ImportResolver));

            return this;
        }

        /// <summary>
        /// Delete an exist template.
        /// </summary>
        /// <param name="templateName">which template should delete.</param>
        /// <returns>updated LG file.</returns>
        public LG DeleteTemplate(string templateName)
        {
            var template = Templates.FirstOrDefault(u => u.Name == templateName);
            if (template != null)
            {
                var startLine = template.ParseTree.Start.Line - 1;
                var stopLine = template.ParseTree.Stop.Line - 1;

                var newContent = ReplaceRangeContent(Content, startLine, stopLine, null);
                Initialize(ParseText(newContent, Id, ImportResolver));
            }

            return this;
        }

        public override string ToString() => Content;

        public override bool Equals(object obj)
        {
            if (!(obj is LG lgFileObj))
            {
                return false;
            }

            return this.Id == lgFileObj.Id && this.Content == lgFileObj.Content;
        }

        public override int GetHashCode() => (Id, Content).GetHashCode();

        private string ReplaceRangeContent(string originString, int startLine, int stopLine, string replaceString)
        {
            var originList = originString.Split('\n');
            var destList = new List<string>();
            if (startLine < 0 || startLine > stopLine || stopLine >= originList.Length)
            {
                throw new Exception("index out of range.");
            }

            destList.AddRange(TrimList(originList.Take(startLine).ToList()));

            if (stopLine < originList.Length - 1)
            {
                destList.Add("\r\n");
                if (!string.IsNullOrEmpty(replaceString))
                {
                    destList.Add(replaceString);
                    destList.Add("\r\n");
                }

                destList.AddRange(TrimList(originList.Skip(stopLine + 1).ToList()));
            }
            else
            {
                // insert at the tail of the content
                if (!string.IsNullOrEmpty(replaceString))
                {
                    destList.Add("\r\n");
                    destList.Add(replaceString);
                }
            }

            return BuildNewLGContent(TrimList(destList));
        }

        /// <summary>
        /// trim the newlines at the beginning or at the tail of the array.
        /// </summary>
        /// <param name="input">input array.</param>
        /// <returns>trimed list.</returns>
        private IList<string> TrimList(IList<string> input)
        {
            if (input == null)
            {
                return null;
            }

            var startIndex = 0;
            var endIndex = input.Count;
            for (var i = 0; i < input.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(input[i]?.Trim()))
                {
                    startIndex = i;
                    break;
                }
            }

            for (var i = input.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(input[i]?.Trim()))
                {
                    endIndex = i + 1;
                    break;
                }
            }

            return input.Skip(startIndex).Take(endIndex - startIndex).ToList();
        }

        private string BuildNewLGContent(IList<string> destList)
        {
            var result = new StringBuilder();
            for (var i = 0; i < destList.Count; i++)
            {
                var currentItem = destList[i];
                result.Append(currentItem);
                if (currentItem.EndsWith("\r"))
                {
                    result.Append("\n");
                }
                else if (i < destList.Count - 1 && !currentItem.EndsWith("\r\n"))
                {
                    result.Append("\r\n");
                }
            }

            return result.ToString();
        }

        private string ConvertTemplateBody(string templateBody)
        {
            if (string.IsNullOrWhiteSpace(templateBody))
            {
                return string.Empty;
            }

            var replaceList = templateBody.Split('\n');

            return string.Join("\n", replaceList.Select(u => WrapTemplateBodyString(u)));
        }

        // we will warp '# abc' into '- #abc', to avoid adding additional template.
        private string WrapTemplateBodyString(string replaceItem) => replaceItem.TrimStart().StartsWith("#") ? $"- {replaceItem.TrimStart()}" : replaceItem;

        private string BuildTemplateNameLine(string templateName, List<string> parameters)
        {
            if (parameters == null)
            {
                return $"# {templateName}";
            }
            else
            {
                return $"# {templateName}({string.Join(", ", parameters)})";
            }
        }

        /// <summary>
        /// use an existing LG file to override current object.
        /// </summary>
        /// <param name="lg">Existing LG file.</param>
        private void Initialize(LG lg)
        {
            Templates = lg.Templates;
            Imports = lg.Imports;
            Diagnostics = lg.Diagnostics;
            References = lg.References;
            Content = lg.Content;
            ImportResolver = lg.ImportResolver;
            Id = lg.Id;
            ExpressionParser = lg.ExpressionParser;
        }

        private void CheckErrors()
        {
            if (AllDiagnostics != null)
            {
                var errors = AllDiagnostics.Where(u => u.Severity == DiagnosticSeverity.Error);
                if (errors.Count() != 0)
                {
                    throw new Exception(string.Join("\n", errors));
                }
            }
        }

        private bool GetStrictModeFromOptions(IList<string> options)
        {
            var result = false;
            if (options == null)
            {
                return result;
            }

            var strictModeKey = "@strict";
            foreach (var option in options)
            {
                if (!string.IsNullOrWhiteSpace(option) && option.Contains("="))
                {
                    var index = option.IndexOf('=');
                    var key = option.Substring(0, index).Trim();
                    var value = option.Substring(index + 1).Trim().ToLower();
                    if (key == strictModeKey)
                    {
                        if (value == "true")
                        {
                            result = true;
                        }
                        else if (value == "false")
                        {
                            result = false;
                        }
                    }
                }
            }

            return result;
        }
    }
}
