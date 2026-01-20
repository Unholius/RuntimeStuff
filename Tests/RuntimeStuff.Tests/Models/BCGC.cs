using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeStuff.MSTests.Models
{
    public class BadCodeGoodCodeUpdateData
    {
        /// <summary>
        /// Некорректный (исходный) код.
        /// </summary>
        public string BadCode { get; set; }

        /// <summary>
        /// Корректный код, на который должен быть заменён некорректный.
        /// </summary>
        public string GoodCode { get; set; }

        /// <summary>
        /// Сообщение об ошибке, возникшей при проверке или обработке кодов.
        /// </summary>
        /// <remarks>
        /// Свойство заполняется в процессе валидации и доступно только для чтения извне.
        /// </remarks>
        public string ErrorMessage { get; private set; }
    }
}
