The purpose of this solution is allow you to inject a new parameter into an existing method in a solution and 
then find and inject this parameter into each generation of callers.  This will allow the parameters to be passed
through the layers / up the stack.  This will find references in all projects in a Solution.

Here is an example that adds a new Parameter to the DBBase Constructor and propagates it through the reference tree.

Before:

    public class DBBase
    {
        public DBBase() {}
	}

    public class FirstReference
    {
        public void Level1Reference()
        {
            var test = new DBBase();
        }

        public void Level1ReferenceWithParm(string foo)
        {
            var test = new DBBase();
        }
    }

	public class SecondReference
    {
        public void Level2Reference()
        {
            var l1 = new FirstReference();
            l1.Level1Reference();
        }
        public void Level2ReferenceWithParm()
        {
            var l1 = new FirstReference();
            l1.Level1ReferenceWithParm("foo");
        }
        public void RegularMethodNoReferences()
        { 
        }
    }

After:

    public class DBBase
    {
        public DBBase(MySession session) {}
	}

	public class FirstReference
    {
        public void Level1Reference(MySession session)
        {
            var test = new DBBase(session);
        }

        public void Level1ReferenceWithParm(MySession session, string foo)
        {
            var test = new DBBase(session);
        }
    }

	public class SecondReference
    {
        public void Level2Reference(MySession session)
        {
            var l1 = new FirstReference();
            l1.Level1Reference(session);
        }

        public void Level2ReferenceWithParm(MySession session)
        {
            var l1 = new FirstReference();
            l1.Level1ReferenceWithParm(session, "foo");
        }

        public void RegularMethodNoReferences()
        { 
        }
    }
