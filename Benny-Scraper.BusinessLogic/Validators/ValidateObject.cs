using RecursiveDataAnnotationsValidation;
using System.ComponentModel.DataAnnotations;

namespace Benny_Scraper.BusinessLogic.Validators
{
    public class ValidateObject
    {
        /// <summary>
        /// Validates an object using the DataAnnotations attributes, will recursively go through all properties and validate them as well.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public List<ValidationResult> Validate(object obj)
        {
            RecursiveDataAnnotationValidator validator = new RecursiveDataAnnotationValidator();
            List<ValidationResult> validationErrors = new List<ValidationResult>();

            if (!validator.TryValidateObjectRecursive(obj, validationErrors))
            {
                //Handle errors however you want
                foreach (var error in validationErrors)
                {
                    Console.WriteLine(error.ErrorMessage);
                    validationErrors.Add(error);
                }
            }
            
            return validationErrors;
        }
    }
}
