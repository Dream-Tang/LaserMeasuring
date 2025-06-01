using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace LaserMeasuring
{
    public class MeasurePoint // 测量点
    {
        private float _aValue;// 私有字段（惯例：下划线开头）
        private float _bValue;
        private float _pointThickness;

        public bool isAValueNew = false; // 当新传入值的时候，赋值true，当做过厚度运算后变为false
        public bool isBValueNew = false;


        public string pointName { get; set; }
        public float aValue  // 传感器A测量值
        {
            get {return _aValue; } 
            set { _aValue = value; isAValueNew = true; } 
        }
        public float bValue  // 传感器B测量值
        {
            get { return _bValue; }
            set { _bValue = value; isBValueNew = true; }
        }

        // 自动属性（编译器自动生成私有字段）
        public float abDistance { get; set; } // AB间距

        // 属性设置 
        public float pointThickness // 测量点厚度 = AB间距 - （aValue+bValue）
        { 
            get 
            {
                if (this.isAValueNew & this.isBValueNew) // 避免新旧值混淆，这一次的A值与上一次的B值参与运算
                {
                    this._pointThickness = this.abDistance - this._aValue - this._bValue;
                    this.isAValueNew = false;
                    this.isBValueNew = false;
                }
                return _pointThickness;
            } 
        }

        // 有参构造函数
        public MeasurePoint(string name, float abDistance, float aValue, float bValue)
        {
            this.pointName = name;
            this.aValue = aValue;
            this.bValue = bValue;
            this.isBValueNew = true;
            this.isBValueNew = true;
            this.abDistance = abDistance;
        }
        // 无参构造函数
        public MeasurePoint(string name, float abDistance) 
        {
            this.pointName = name;
            this.abDistance = abDistance;
        }

    }
}
