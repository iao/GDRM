using System; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronPython.Hosting;
using IronPython.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using log4net;
using System.Reflection;
using System.IO;
using GD;

namespace GroupDRegionModule 
{

    enum MethodType { INIT, ACTION, CONVERSATION };
    /// <summary>
    /// A representation of a compiled Python script. The main interface to calling Python scripts.
    /// </summary>
    public class NpcScript
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static Dictionary<string, NpcScript> scripts = new Dictionary<string, NpcScript>();
        private CompiledCode cc = null;
        private CompiledCode init_call = null;
        private CompiledCode action_call = null;
        private CompiledCode conversation_call = null;
        public string filename { get; private set; }

        /// <summary>
        /// Creates an NPCScript instance.
        /// </summary>
        /// <param name="filename">The filename of the script</param>
        /// <param name="cc">The compiled representation of the script</param>
        private NpcScript(string filename, CompiledCode cc)
        {
            this.filename = filename;
            this.cc = cc;
            this.init_call = NPCManager.GetInstance().GetEngine().CreateScriptSourceFromString("init(ctx)").Compile();
            this.action_call = NPCManager.GetInstance().GetEngine().CreateScriptSourceFromString("action(ctx)").Compile();
            this.conversation_call = NPCManager.GetInstance().GetEngine().CreateScriptSourceFromString("converse(ctx, cnv)").Compile();
        }

        /// <summary>
        /// Loads a script from a file, including compilation, returning an NPCScript instance
        /// </summary>
        /// <param name="filename">The filename of the script</param>
        /// <returns>A constructed NPCScript instance</returns>
        public static NpcScript loadFromFile(string filename)
        {
            lock (scripts)
            {
                NpcScript ret;
                if (scripts.TryGetValue(filename, out ret))
                {
                    return ret;
                }
                else 
                {
                    string script_contents = GDRMTools.loadFile(GroupDRegionModule.SCRIPTS_DIR + Path.DirectorySeparatorChar + filename);
                    if (script_contents != null)
                    {
                        m_log.InfoFormat("Loaded script file {0}", filename);

                        try
                        {
                            // Create a scope, load script
                            ScriptSource script_source = NPCManager.GetInstance().GetEngine().CreateScriptSourceFromString(script_contents);
                            CompiledCode cc = script_source.Compile();
                            // Add to the script cache
                            ret = new NpcScript(filename, cc);
                            scripts.Add(filename, ret);
                            return ret;
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[GDRM: NpcScript] Error compiling script {0}", e.ToString());
                            return null;
                        }

                    }
                    else
                    {
                        // Error loading file
                        m_log.InfoFormat("Error loading script file {0}", filename);
                        return null;
                    }
                }
            } 
        }

        /// <summary>
        /// Invokes the init() method in the script
        /// </summary>
        /// <param name="context">The NPCContext for the NPC</param>
        public void runInit(NPCharacter context)
        {
            lock (context)
            {
                // Get a scope populated with the appropriate variables / methods
                ScriptScope populated_scope = populateScope(context, null);
                if (populated_scope.ContainsVariable("init"))
                {
                    // Call initialisation method

                    try
                    {
                        init_call.Execute(populated_scope);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("Error running script (init) {0}: {1}", context.script.filename, e.ToString());
                        context.SetDisposing();
                    }
                }
                else
                {
                    // Init method not found, log error
                    m_log.ErrorFormat("Attempted to run nonexistent init method in script {0}", filename);

                }
            }
        }

        /// <summary>
        /// Invokes the action() method in the script
        /// </summary>
        /// <param name="context">The NPCContext for the NPC</param>
        public void runAction(NPCharacter context)
        {
            lock (context)
            {
                // Get a scope populated with the appropriate variables / methods
                ScriptScope populated_scope = populateScope(context, null);
                if (populated_scope.ContainsVariable("action"))
                {
                    try
                    {
                        action_call.Execute(populated_scope);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("Error running script (action) {0}: {1}", context.script.filename, e.ToString());
                        context.SetDisposing();
                    }
                }
                else
                {
                    // Init method not found, log error
                    m_log.ErrorFormat("Attempted to run nonexistent action method in script {0}", filename);
                }
            }
        }

        /// <summary>
        /// Invokes the converse() method in the script
        /// </summary>
        /// <param name="context">The NPCContext for the NPC</param>
        /// <param name="conversation">The NPCConversation for the user conversing with the NPC</param>
        public void runConversation(NPCharacter context, NpcConversation conversation)
        {
            lock (context)
            {
                // Get a scope populated with the appropriate variables / methods
                ScriptScope populated_scope = populateScope(context, conversation);
                if (populated_scope.ContainsVariable("converse"))
                {
                    try
                    {
                        // Call converse method
                        conversation_call.Execute(populated_scope);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("Error running script (action) {0}: {1}", context.script.filename, e.ToString());
                        context.SetDisposing();
                    }
                }
                else
                {
                    // Init method not found, log error
                    m_log.ErrorFormat("Attempted to run nonexistent conversation method in script {0}", filename);
                }
            }
        }

        /// <summary>
        /// Loads the context and optionally the conversation into the current script scope.
        /// Must be called prior to method invocation.
        /// </summary>
        /// <param name="npc_context">The NPC Context</param>
        /// <param name="conversation">The NPCConversation</param>
        /// <returns>A populated script scope</returns>
        private ScriptScope populateScope (NPCharacter npc_context, NpcConversation conversation)
        {
            ScriptEngine engine = NPCManager.GetInstance().GetEngine();
            ScriptScope ss = engine.CreateScope();
            ss.SetVariable("ctx", (INpcContext) npc_context);
            ss.SetVariable("cnv", conversation);
            try
            {
                cc.Execute(ss); // Populates the scope with all of the defined functions
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[GDRM: NPCScript]: Error populating script scope: {0}", e.ToString());
            }
            return ss;
        }

        /// <summary>
        /// Clears the script cache
        /// </summary>
        public static void ClearScriptCache()
        {
            lock (scripts)
            {
                scripts.Clear();
            }
        }


    }
}
