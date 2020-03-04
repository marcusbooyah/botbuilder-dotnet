using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AdaptiveExpressions;
using Newtonsoft.Json.Linq;

namespace Microsoft.Bot.Builder.LanguageGeneration
{
    public class LGFunction
    {
        private const string ReExecuteSuffix = "!";
        private static readonly Regex ExpressionRecognizeRegex = new Regex(@"(?<!\\)\${((\'[^\r\n\']*\')|(\""[^\""\r\n]*\"")|(\`(\\\`|[^\`])*\`)|([^\r\n{}'""`]))+}?", RegexOptions.Compiled);
        private LG lg;

        public LGFunction(LG lg)
        {
            this.lg = lg;
        }

        private EvaluatorLookup CustomizedEvaluatorLookup(EvaluatorLookup baseLookup)
        => (string name) =>
        {
            var standardFunction = baseLookup(name);

            if (standardFunction != null)
            {
                return standardFunction;
            }

            if (name.StartsWith("lg."))
            {
                name = name.Substring(3);
            }

            var templateName = ParseTemplateName(name).pureTemplateName;

            if (this.lg.Templates.Any(u => u.Name == templateName))
            {
                return new ExpressionEvaluator(templateName, ExpressionFunctions.Apply(this.TemplateEvaluator(name)), ReturnType.Object, this.ValidTemplateReference);
            }

            const string template = "template";

            if (name.Equals(template))
            {
                return new ExpressionEvaluator(template, ExpressionFunctions.Apply(this.TemplateFunction()), ReturnType.Object, this.ValidateTemplateFunction);
            }

            const string fromFile = "fromFile";

            if (name.Equals(fromFile))
            {
                return new ExpressionEvaluator(fromFile, ExpressionFunctions.Apply(this.FromFile()), ReturnType.String, ExpressionFunctions.ValidateUnaryString);
            }

            const string activityAttachment = "ActivityAttachment";

            if (name.Equals(activityAttachment))
            {
                return new ExpressionEvaluator(
                    activityAttachment,
                    ExpressionFunctions.Apply(this.ActivityAttachment()),
                    ReturnType.Object,
                    (expr) => ExpressionFunctions.ValidateOrder(expr, null, ReturnType.Object, ReturnType.String));
            }

            const string isTemplate = "isTemplate";

            if (name.Equals(isTemplate))
            {
                return new ExpressionEvaluator(isTemplate, ExpressionFunctions.Apply(this.IsTemplate()), ReturnType.Boolean, ExpressionFunctions.ValidateUnaryString);
            }

            return null;
        };

        private Func<IReadOnlyList<object>, object> IsTemplate()
       => (IReadOnlyList<object> args) =>
       {
           var templateName = args[0].ToString();
           return this.lg.Templates.Any(u => u.Name == templateName);
       };

        private Func<IReadOnlyList<object>, object> ActivityAttachment()
        => (IReadOnlyList<object> args) =>
        {
            return new JObject
            {
                ["lgType"] = "attachment",
                ["contenttype"] = args[1].ToString(),
                ["content"] = args[0] as JObject
            };
        };

        private Func<IReadOnlyList<object>, object> FromFile()
       => (IReadOnlyList<object> args) =>
       {
           var filePath = args[0].ToString().NormalizePath();

           var resourcePath = GetResourcePath(filePath);
           var stringContent = File.ReadAllText(resourcePath);

           var evaluator = new MatchEvaluator(m => EvalExpression(m.Value).ToString());
           var result = ExpressionRecognizeRegex.Replace(stringContent, evaluator);
           return result.Escape();
       };

        private string GetResourcePath(string filePath)
        {
            string resourcePath;

            if (Path.IsPathRooted(filePath))
            {
                resourcePath = filePath;
            }
            else
            {
                var template = TemplateMap[CurrentTarget().TemplateName];
                var sourcePath = template.Source.NormalizePath();
                var baseFolder = Environment.CurrentDirectory;
                if (Path.IsPathRooted(sourcePath))
                {
                    baseFolder = Path.GetDirectoryName(sourcePath);
                }

                resourcePath = Path.GetFullPath(Path.Combine(baseFolder, filePath));
            }

            return resourcePath;
        }

        // Evaluator for template(templateName, ...args) 
        // normal case we can just use templateName(...args), but template function is particularly useful when the template name is not pre-known
        private Func<IReadOnlyList<object>, object> TemplateFunction()
        => (IReadOnlyList<object> args) =>
        {
            var templateName = args[0].ToString();
            var newScope = this.ConstructScope(templateName, args.Skip(1).ToList());
            return this.EvaluateTemplate(templateName, newScope);
        };

        // Validator for template(...)
        private void ValidateTemplateFunction(Expression expression)
        {
            ExpressionFunctions.ValidateAtLeastOne(expression);

            var children0 = expression.Children[0];

            if (children0.ReturnType != ReturnType.Object && children0.ReturnType != ReturnType.String)
            {
                throw new Exception(LGErrors.ErrorTemplateNameformat(children0.ToString()));
            }

            // Validate more if the name is string constant
            if (children0.Type == ExpressionType.Constant)
            {
                var templateName = (children0 as Constant).Value.ToString();
                CheckTemplateReference(templateName, expression.Children.Skip(1));
            }
        }

        private Func<IReadOnlyList<object>, object> TemplateEvaluator(string templateName)
        => (IReadOnlyList<object> args) =>
        {
            var newScope = this.ConstructScope(templateName, args.ToList());
            return this.EvaluateTemplate(templateName, newScope);
        };

        private void ValidTemplateReference(Expression expression)
        {
            CheckTemplateReference(expression.Type, expression.Children);
        }

        private void CheckTemplateReference(string templateName, IEnumerable<Expression> children)
        {
            var template = this.lg.Templates.FirstOrDefault(u => u.Name == templateName);
            if (template == null)
            {
                throw new Exception(LGErrors.TemplateNotExist(templateName));
            }

            var expectedArgsCount = template.Parameters.Count();
            var actualArgsCount = children.Count();

            if (actualArgsCount != 0 && expectedArgsCount != actualArgsCount)
            {
                throw new Exception(LGErrors.ArgumentMismatch(templateName, expectedArgsCount, actualArgsCount));
            }
        }

        private (bool reExecute, string pureTemplateName) ParseTemplateName(string templateName)
        {
            if (templateName == null)
            {
                throw new ArgumentException("template name is null.");
            }

            return templateName.EndsWith(ReExecuteSuffix) ?
                (true, templateName.Substring(0, templateName.Length - ReExecuteSuffix.Length))
                : (false, templateName);
        }
    }
}
