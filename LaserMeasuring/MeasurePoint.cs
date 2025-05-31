using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaserMeasuring
{
    public class MeasurePoint // 测量点
    {       
        public string pointNum;// 私有字段（惯例：下划线开头）

        public double aValue { get; set; } // 传感器A测量值
        public double bValue { get; set; } // 传感器B测量值

        public double abDistance { get; set; } // AB间距

        // 属性设置 
        private double pointThickness // 测量点厚度 = AB间距 - （aValue+bValue）
        { 
            get { return pointThickness; }
            set { pointThickness = abDistance - aValue - bValue; } 
        }

        // 有参构造函数
        public MeasurePoint(double aValue, double bValue)
        {
            this.aValue = aValue;
            this.bValue = bValue;
        }
        // 无参构造函数
        public MeasurePoint() { }

    }
}
