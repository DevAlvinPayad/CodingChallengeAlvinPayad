using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunctionApp.Models
{
    internal class Employee
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "firstName")]
        [Required]
        public string FirstName { get; set; }

        [JsonProperty(PropertyName = "lastName")]
        [Required]
        public string LastName { get; set; }

        [JsonProperty(PropertyName = "birthdayInEpoch")]
        [Required]
        public string BirthdayInEpoch { get; set; }

        [JsonProperty(PropertyName = "email")]
        [Required]
        public string Email { get; set; }
    }
}
