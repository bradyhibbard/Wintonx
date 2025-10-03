namespace Winton.Models
{
    public class Section
    {
        public string SectionID { get; set; }
        public string Name { get; set; }


        /// <summary>
        /// Creates a clone of this section with a new SectionID.
        /// </summary>
        public Section Clone()
        {
            return new Section
            {
                SectionID = Guid.NewGuid().ToString(),
                Name = this.Name            
            };
        }
    }
}
