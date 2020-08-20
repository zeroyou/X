﻿using System;
using System.Collections.Generic;
using NewLife.Caching;
using NewLife.Log;
using Xunit;
using NewLife.Serialization;
using NewLife.Data;
using System.Diagnostics;
using System.Threading;
using NewLife;
using System.Linq;

namespace XUnitTest.Caching
{
    public class RedisTest
    {
        public Redis Redis { get; set; }

        public RedisTest()
        {
            //Redis = Redis.Create("127.0.0.1:6379", "newlife", 4);
            //Redis = Redis.Create("127.0.0.1:6379", null, 4);
            Redis = new Redis("127.0.0.1:6379", null, 4);
#if DEBUG
            Redis.Log = XTrace.Log;
#endif
        }

        [Fact(DisplayName = "基础测试")]
        public void BasicTest()
        {
            var ic = Redis;
            var key = "Name";
            var key2 = "Company";

            ic.Set(key, "大石头");
            ic.Set(key2, "新生命");
            Assert.Equal("大石头", ic.Get<String>(key));
            Assert.Equal("新生命", ic.Get<String>(key2));

            var count = ic.Count;
            Assert.True(count >= 2);

            // Keys
            var keys = ic.Keys;
            Assert.True(keys.Contains(key));

            // 过期时间
            ic.SetExpire(key, TimeSpan.FromSeconds(1));
            var ts = ic.GetExpire(key);
            Assert.True(ts.TotalSeconds > 0 && ts.TotalSeconds < 2, "过期时间");

            var rs = ic.Remove(key2);
            if (ic.AutoPipeline > 0) rs = (Int32)ic.StopPipeline(true)[0];
            Assert.Equal(1, rs);

            Assert.False(ic.ContainsKey(key2));

            ic.Clear();
            ic.StopPipeline(true);
            Assert.True(ic.Count == 0);
        }

        [Fact(DisplayName = "集合测试")]
        public void DictionaryTest()
        {
            var ic = Redis;

            var dic = new Dictionary<String, String>
            {
                ["111"] = "123",
                ["222"] = "abc",
                ["大石头"] = "学无先后达者为师"
            };

            ic.SetAll(dic);
            var dic2 = ic.GetAll<String>(dic.Keys);

            Assert.Equal(dic.Count, dic2.Count);
            foreach (var item in dic)
            {
                Assert.Equal(item.Value, dic2[item.Key]);
            }
        }

        [Fact(DisplayName = "高级添加")]
        public void AddReplace()
        {
            var ic = Redis;
            var key = "Name";

            ic.Set(key, Environment.UserName);
            var rs = ic.Add(key, Environment.MachineName);
            Assert.False(rs);

            var name = ic.Get<String>(key);
            Assert.Equal(Environment.UserName, name);
            Assert.NotEqual(Environment.MachineName, name);

            var old = ic.Replace(key, Environment.MachineName);
            Assert.Equal(Environment.UserName, old);

            name = ic.Get<String>(key);
            Assert.Equal(Environment.MachineName, name);
            Assert.NotEqual(Environment.UserName, name);
        }

        [Fact(DisplayName = "累加累减")]
        public void IncDec()
        {
            var ic = Redis;
            var key = "CostInt";
            var key2 = "CostDouble";

            ic.Set(key, 123);
            ic.Increment(key, 22);
            Assert.Equal(123 + 22, ic.Get<Int32>(key));

            ic.Set(key2, 456d);
            ic.Increment(key2, 22d);
            Assert.Equal(456d + 22d, ic.Get<Double>(key2));
        }

        [Fact(DisplayName = "复杂对象")]
        public void TestObject()
        {
            var obj = new User
            {
                Name = "大石头",
                Company = "NewLife",
                Age = 24,
                Roles = new[] { "管理员", "游客" },
                UpdateTime = DateTime.Now,
            };

            var ic = Redis;
            var key = "user";

            ic.Set(key, obj);
            var obj2 = ic.Get<User>(key);

            Assert.Equal(obj.ToJson(), obj2.ToJson());
        }

        class User
        {
            public String Name { get; set; }
            public String Company { get; set; }
            public Int32 Age { get; set; }
            public String[] Roles { get; set; }
            public DateTime UpdateTime { get; set; }
        }

        [Fact(DisplayName = "字节数组")]
        public void TestBuffer()
        {
            var ic = Redis;
            var key = "buf";

            var str = "学无先后达者为师";
            var buf = str.GetBytes();

            ic.Set(key, buf);
            var buf2 = ic.Get<Byte[]>(key);

            Assert.Equal(buf.ToHex(), buf2.ToHex());
        }

        [Fact(DisplayName = "数据包")]
        public void TestPacket()
        {
            var ic = Redis;
            var key = "buf";

            var str = "学无先后达者为师";
            var pk = new Packet(str.GetBytes());

            ic.Set(key, pk);
            var pk2 = ic.Get<Packet>(key);

            Assert.Equal(pk.ToHex(), pk2.ToHex());
        }

        [Fact(DisplayName = "管道")]
        public void TestPipeline()
        {
            var ap = Redis.AutoPipeline;
            Redis.AutoPipeline = 100;

            BasicTest();

            Redis.AutoPipeline = ap;
        }

        [Fact(DisplayName = "管道2")]
        public void TestPipeline2()
        {
            var ap = Redis.AutoPipeline;
            Redis.AutoPipeline = 100;

            var ic = Redis;
            var key = "Name";
            var key2 = "Company";

            ic.Set(key, "大石头");
            ic.Set(key2, "新生命");
            var ss = ic.StopPipeline(true);
            Assert.Equal("OK", ss[0]);
            Assert.Equal("OK", ss[1]);
            Assert.Equal("大石头", ic.Get<String>(key));
            Assert.Equal("新生命", ic.Get<String>(key2));

            var count = ic.Count;
            Assert.True(count >= 2);

            // Keys
            var keys = ic.Keys;
            Assert.True(keys.Contains(key));

            // 过期时间
            ic.SetExpire(key, TimeSpan.FromSeconds(1));
            var ts = ic.GetExpire(key);
            Assert.True(ts.TotalSeconds > 0 && ts.TotalSeconds < 2, "过期时间");

            var rs = ic.Remove(key2);
            if (ic.AutoPipeline > 0) rs = (Int32)ic.StopPipeline(true)[0];
            Assert.Equal(1, rs);

            Assert.False(ic.ContainsKey(key2));

            ic.Clear();
            ic.StopPipeline(true);
            Assert.True(ic.Count == 0);

            Redis.AutoPipeline = ap;
        }

        [Fact(DisplayName = "正常锁")]
        public void TestLock1()
        {
            var ic = Redis;

            var ck = ic.AcquireLock("lock:TestLock1", 3000);
            var k2 = ck as CacheLock;

            Assert.NotNull(k2);
            Assert.Equal("lock:TestLock1", k2.Key);

            // 实际上存在这个key
            Assert.True(ic.ContainsKey(k2.Key));

            // 取有效期
            var exp = ic.GetExpire(k2.Key);
            Assert.True(exp.TotalMilliseconds <= 3000);

            // 释放锁
            ck.Dispose();

            // 这个key已经不存在
            Assert.False(ic.ContainsKey(k2.Key));
        }

        [Fact(DisplayName = "抢锁失败")]
        public void TestLock2()
        {
            var ic = Redis;

            var ck1 = ic.AcquireLock("lock:TestLock2", 3000);
            // 故意不用using，验证GC是否能回收
            //using var ck1 = ic.AcquireLock("TestLock2", 3000);

            var sw = Stopwatch.StartNew();

            // 抢相同锁，不可能成功。超时时间必须小于3000，否则前面的锁过期后，这里还是可以抢到的
            Assert.Throws<InvalidOperationException>(() => ic.AcquireLock("lock:TestLock2", 2000));

            // 耗时必须超过有效期
            sw.Stop();
            XTrace.WriteLine("TestLock2 ElapsedMilliseconds={0}ms", sw.ElapsedMilliseconds);
            Assert.True(sw.ElapsedMilliseconds >= 2000);

            Thread.Sleep(3000 - 2000 + 1);

            // 那个锁其实已经不在了，缓存应该把它干掉
            Assert.False(ic.ContainsKey("lock:TestLock2"));
        }

        [Fact(DisplayName = "抢死锁")]
        public void TestLock3()
        {
            var ic = Redis;

            using var ck = ic.AcquireLock("TestLock3", 3000);

            // 已经过了一点时间
            Thread.Sleep(2000);

            // 循环多次后，可以抢到
            using var ck2 = ic.AcquireLock("TestLock3", 3000);
            Assert.NotNull(ck2);
        }

        [Fact(DisplayName = "搜索测试")]
        public void SearchTest()
        {
            var ic = Redis;

            // 添加删除
            ic.Set("username", Environment.UserName, 60);

            //var ss = ic.Search("*");
            var ss = ic.Execute(null, r => r.Execute<String[]>("KEYS", "*"));
            Assert.NotNull(ss);
            Assert.NotEmpty(ss);

            var ss2 = ic.Execute(null, r => r.Execute<String[]>("KEYS", "abcdefg*"));
            Assert.NotNull(ss2);
            Assert.Empty(ss2);

            var n = 0;
            var ss3 = Search(ic, "*", 10, ref n);
            //var ss3 = ic.Execute(null, r => r.Execute<Object[]>("SCAN", n, "MATCH", "*", "COUNT", 10));
            Assert.NotNull(ss3);
            Assert.NotEmpty(ss3);

            var ss4 = Search(ic, "wwee*", 10, ref n);
            //var ss4 = ic.Execute(null, r => r.Execute<Object[]>("SCAN", n, "MATCH", "wwee*", "COUNT", 10));
            Assert.NotNull(ss4);
            Assert.Empty(ss4);
        }

        private String[] Search(Redis rds, String pattern, Int32 count, ref Int32 position)
        {
            var p = position;
            var rs = rds.Execute(null, r => r.Execute<Object[]>("SCAN", p, "MATCH", pattern + "", "COUNT", count));

            if (rs != null)
            {
                position = (rs[0] as Packet).ToStr().ToInt();

                var ps = rs[1] as Object[];
                var ss = ps.Select(e => (e as Packet).ToStr()).ToArray();
                return ss;
            }

            return null;
        }
    }
}